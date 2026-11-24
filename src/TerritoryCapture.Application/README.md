# Application — contributor B

## Delivery scope

B's game controller, session/view lifecycle management, browser-safe computer turn runner, unit test doubles, and independent xUnit suite are implemented and merged into `main`, alongside the production Web DI wiring fix in `src/TerritoryCapture.Web/Program.cs`.

Implemented:

- `GameController : IGameController, IDisposable`: orchestrates `Setup`, `Game`, and `Result` screen transitions, publishes immutable `GameView` snapshots with `ViewToken` (`SessionId` + `Revision`) gating, computes `CanSave`, `LegalColors`, `Players` scores, `NeutralCount`, and `WinnerIds` from `IGameRules`, and runs consecutive computer turns asynchronously without blocking the UI thread.
- `tests/TerritoryCapture.Application.Tests`: isolated xUnit suite (`GameControllerTests`) using dedicated test doubles (`FakeGameRules`, `ControllableComputerStrategy`, `InMemorySaveStore`) so controller orchestration is verified independently of concrete rules and storage implementations.
- `src/TerritoryCapture.Web/Program.cs`: removed the `#if DEBUG` / `IsDevelopment()` `DemoController` override so local development and release builds both resolve the real `IGameRules`, `IComputerStrategy`, `IGameSaveStore`, and `IGameController`.

Pending for final Milestone 3 sign-off:

- `tests/TerritoryCapture.Integration.Tests`: controller-driven integration tests wired against A's real `GameRules` and D's real `GreedyComputerStrategy` / codec / store adapters.
- Remaining controller hardening items noted under Verification (granular `StoreError` mapping and stale-load guard in `ResumeAsync`, plus surfacing unexpected `RuleError` during `RunComputerLoopAsync` as `BackgroundError`).
- Final F01–F08 / UC01–UC07 acceptance checklist mapped to real-component automated and manual checks.

## Run locally

Install the SDK matching the root `global.json`, then run from the repository root:

```sh
dotnet restore TerritoryCapture.sln --locked-mode
dotnet test tests/TerritoryCapture.Application.Tests --configuration Release --no-restore
```

To run the full solution test suite (including Rules, Infrastructure, and Integration once populated):

```sh
dotnet test TerritoryCapture.sln --configuration Release --no-restore
```

The application unit suite contains 14 test cases covering `GameController` (84% code coverage measured with Rider dotCover). Tests use `FakeGameRules`, `ControllableComputerStrategy`, and `InMemorySaveStore` to verify startup validation, stale-token rejection, command gating, consecutive computer turns stopping at a human turn, mid-turn cancellation via `ReturnToSetupAsync`, save/resume policies, and disposal.

## Registration and UI handoff for C

Register the controller as scoped in the Blazor WebAssembly composition root (`src/TerritoryCapture.Web/Program.cs`) alongside A's rules and D's infrastructure services:

```csharp
using TerritoryCapture.Application;
using TerritoryCapture.Contracts;
using TerritoryCapture.Infrastructure;
using TerritoryCapture.Rules;

builder.Services.AddScoped<IGameRules, GameRules>();
builder.Services.AddScoped<IComputerStrategy, GreedyComputerStrategy>();
builder.Services.AddTerritoryCaptureInfrastructure();
builder.Services.AddScoped<IGameController, GameController>();
```

`GameController` must be registered as `Scoped` (never `Transient`) so all views share the same active session and the DI container invokes `Dispose()` on teardown. Keep `DemoController` and other development fakes out of startup wiring.

UI integration rules for Blazor components:

- Subscribe to `IGameController.Changed` and marshal UI updates through `InvokeAsync(StateHasChanged)`. Unsubscribe in the component's `Dispose` method.
- Always pass the currently rendered `Controller.View.Token` to `ChooseColorAsync` and `SaveAsync`. Stale clicks are rejected deterministically with `CommandError.StaleView`.
- Render color controls from `View.LegalColors` and bind the save button's enabled state to `View.CanSave`. Both are automatically empty/`false` while `View.IsBusy` is `true`, during computer turns, or on `Screen.Result`.
- Surface both synchronous `CommandResult.Error` responses and `View.BackgroundError` (background computer loop failures).

## Controller lifecycle and command contract

`GameController` starts in `Screen.Setup` (`Revision = 0`, `State = null`, `IsBusy = false`, `CanSave = false`). Every published view transition increments `Token.Revision`; starting, resuming, or returning to setup assigns a fresh `Guid` `Token.SessionId` and cancels any in-flight computer runner via `CancellationTokenSource`.

| Command | Preconditions | Effect on success | Error outcomes |
| --- | --- | --- | --- |
| `StartAsync(options)` | `Screen.Setup`, `!IsBusy` | Generates seed (`Random.Shared.Next()`), calls `IGameRules.Create`, starts a new session (`Screen.Game` or `Screen.Result`), triggers computer runner if player 0 is `Computer` | `NotAllowed`, `InvalidOptions` |
| `ChooseColorAsync(expected, color)` | Matching `ViewToken`, `Screen.Game`, `!IsBusy`, `Phase == Running`, current player is `Human` | Calls `IGameRules.ApplyMove`, publishes updated view (`Revision + 1`), triggers computer runner asynchronously without awaiting computer turns | `StaleView`, `NotAllowed`, `IllegalColor` |
| `SaveAsync(expected)` | Matching `ViewToken`, `CanSave == true` | Publishes `IsBusy = true`, persists version-1 `GameSnapshot` via `IGameSaveStore.SaveAsync`, restores `IsBusy = false` if session/revision still match | `StaleView`, `NotAllowed`, `StorageUnavailable`, `UnexpectedFailure` |
| `ResumeAsync()` | `Screen.Setup`, `!IsBusy` | Publishes `IsBusy = true`, loads via `IGameSaveStore.LoadAsync`, validates with `IGameRules.Validate` plus running human-turn policy, starts a new session | `NotAllowed`, `MissingSave`, `InvalidSave` (target: also `UnsupportedSaveVersion`, `StorageUnavailable`) |
| `ReturnToSetupAsync()` | Any state | Cancels active computer runner, publishes fresh `Screen.Setup` view with new `SessionId` | `None` |

### View derivation (`BuildGameView`)

- `isHumanTurn`: `state.Phase == GamePhase.Running && state.Players[state.CurrentPlayerIndex].Kind == PlayerKind.Human`
- `CanSave`: `!isBusy && isHumanTurn && screen == Screen.Game`
- `LegalColors`: `canSave ? _gameRules.LegalColors(state) : ImmutableArray<GameColor>.Empty`
- `Players`, `NeutralCount`, `WinnerIds`: derived solely from `IGameRules.Summarize(state)` — no score or territory logic is duplicated in the Application layer.

## Dependencies, computer loop, and storage handoff

`TerritoryCapture.Application` depends only on `TerritoryCapture.Contracts` (no project references to `Rules`, `Infrastructure`, or `Web`):

- **Rules (`IGameRules` — contributor A):** owns board creation, legal color calculation, move application, state validation, and scoring/winner summary. The controller never mutates board cells or implements ending/anti-stalling rules.
- **Strategy (`IComputerStrategy` — contributor D):** invoked inside `RunComputerLoopAsync` on each computer turn.
- **Storage (`IGameSaveStore` — contributor D):** owns version-1 JSON encoding/decoding and `localStorage` persistence.

### Computer turn runner

- `CheckAndStartComputerRunner(sessionId)` starts `RunComputerLoopAsync` after `StartNewSession` and after every successful human `ChooseColorAsync` whenever the next active player is `PlayerKind.Computer`.
- `RunComputerLoopAsync` yields between turns with `await Task.Delay(1, cancelToken)` (never `Task.Run`) so it remains WebAssembly-safe and lets Blazor render intermediate board updates.
- Before and after each await, the loop verifies `!cancelToken.IsCancellationRequested` and `_gameView.Token.SessionId == sessionId` so cancelled or superseded sessions can never overwrite a new game or setup screen.
- `OperationCanceledException` exits cleanly; unexpected exceptions publish `CommandError.UnexpectedFailure` into `View.BackgroundError` if the session is still active.

### Save and resume policy boundary

Per Infrastructure's codec handoff, a successful `LoadAsync` only proves version-1 wire shape. Before installing a loaded state, `ResumeAsync` enforces:

1. `IGameRules.Validate(state).IsValid`
2. `state.Phase == GamePhase.Running`
3. `state.Players[state.CurrentPlayerIndex].Kind == PlayerKind.Human`

If any check fails, the controller returns to `Screen.Setup` with an error and never deletes the existing stored save.

## Verification — November 24, 2026

Verified locally on `main` after integration bugfixes:

- `dotnet test tests/TerritoryCapture.Application.Tests`: 14 passed, 0 failed, 0 skipped; 84% coverage on `GameController`.
- Manual browser verification (`TerritoryCapture.Web`) with real `GameRules`, `GreedyComputerStrategy`, `BrowserGameSaveStore`, and `GameController` wired in `Program.cs`:
    - Starting a game renders a randomized board from `GameRules`.
    - Legal color buttons appear on human turns and apply moves correctly.
    - Computer turns trigger automatically after human moves.
- Remaining before final release acceptance (due November 30, 2026):
    - Map `StoreError.UnsupportedVersion` and `StoreError.Unavailable` distinctly in `ResumeAsync` and ignore late `LoadAsync` completions if `ReturnToSetupAsync` was called while loading.
    - Publish `CommandError.UnexpectedFailure` (and block further moves/saves) if `RunComputerLoopAsync` encounters a non-`None` `RuleError`.
    - Add real-component integration tests in `tests/TerritoryCapture.Integration.Tests` and record full Chrome/Edge acceptance evidence for F01–F08 and UC01–UC07.