# Architecture and work plan for four contributors

This is the implementation plan for the requirements in [specification.tex](specification.tex), including F01–F08 and UC01–UC07. It divides work by independently testable components rather than by screens or successive phases. Contributors A–D are placeholders for the four team members. Assign names once; the responsibilities below then remain stable.

The official assignment permits C# with MAUI, WinUI or Blazor. This repository has already selected **C#, .NET 10, standalone Blazor WebAssembly**, and this plan keeps that decision. The application runs in the browser and can be deployed to a static HTTP(S) host. No backend, account system or database is required.

## 1. Scope and decisions to freeze first

The specification is the acceptance baseline. Keep these decisions in the first shared contract review:

| Topic | Required behavior |
| --- | --- |
| Participants | 2–4 players, each human or computer, at least one human; list order is turn order. |
| Board | 12 columns × 10 rows, six colors, no wrapping; adjacency means a shared hex edge. |
| Initial territory | Different corner starts and different player colors; initially capture the entire same-color component connected to each start. |
| Move | Recolor the player's **whole territory**, then capture all reachable neutral cells of the chosen color. Opponent cells cannot be captured or traversed. |
| Legal colors | Exclude every player's current color, including the acting player's own color. |
| Turn | Exactly one complete move, then the next participant. Invalid input leaves state and order unchanged. A legal zero-gain move still advances the turn. |
| Computer | Greedy full-component gain; ties use the fixed palette order below. Zero gain still produces a legal move. |
| End | Finish when there are no neutral cells, including immediately after initialization. All players with the maximum score win; multiple winners mean a draw. |
| Persistence | One versioned JSON save in `localStorage`; save only during an idle human turn. Resume from setup, preserving the board and next player exactly. |
| New game | Return to setup, invalidate pending computer work, and keep the previous saved game. An interrupted game has no winner. |
| Presentation | SVG board, owners and colors, current player, scores and neutral count; current stable desktop Chrome/Edge at 1280 × 720 or larger. |

The following implementation choices resolve details left open in the specification; they do not add features:

- IDs are zero-based. Cell ID is `row * 12 + column`; player ID equals its initial position in the player list and never changes.
- Use pointy-top hexes with **odd rows shifted right** (odd-r coordinates). See section 3 for the exact neighbor convention.
- Assign starts in this order: `(0,0)`, `(11,9)`, `(11,0)`, `(0,9)`. Take the first N for N players. Two players therefore start at opposite corners.
- Freeze palette IDs and tie order: `Red=0, Orange=1, Yellow=2, Green=3, Blue=4, Purple=5`. UI CSS colors may change without changing these IDs.
- Generate board colors uniformly from those six IDs; assign distinct colors to start cells by sampling without replacement, then compute initial territories. A seed is an internal testing parameter, not a user-facing feature.
- JSON version is `1`; storage key is `territory-capture.save.v1`. Do not add difficulty levels, networking, a board editor or alternative platforms before the specified workflow is complete.

The official wording says play continues while there is capturable territory. The specification makes this precise as “neutral cells remain.” With six colors and at most four distinct player colors, a legal color always exists; a zero-gain move is not a stopping condition. Do not introduce a “no immediate gain” ending rule.

## 2. Project boundaries and ownership

Create one solution with the following projects. These are proposed paths, not files already implemented in this checkout.

```text
src/
  TerritoryCapture.Contracts/       Shared immutable types and interfaces
  TerritoryCapture.Rules/           A: pure rules and board generation
  TerritoryCapture.Application/     B: controller/facade and view projection
  TerritoryCapture.Infrastructure/  D: greedy strategy, JSON and browser storage
  TerritoryCapture.Web/             C: Blazor components and composition root
tests/
  TerritoryCapture.Rules.Tests/           A
  TerritoryCapture.Application.Tests/     B
  TerritoryCapture.Infrastructure.Tests/  D
  TerritoryCapture.Web.Tests/             C
  TerritoryCapture.Integration.Tests/     B coordinates; all contribute cases
```

All projects target `net10.0`; Web uses the Blazor WebAssembly SDK. Use xUnit for ordinary .NET tests and bUnit for component tests. Add a small Playwright browser suite for actual WebAssembly and `localStorage` behavior. Pin compatible dependency versions and the SDK when creating the solution; commit dependency lockfiles and use locked restore in CI.

```mermaid
flowchart TD
    Web[Web: Blazor views and DI composition] --> App[Application: game controller / Facade]
    Web --> Infra[Infrastructure: greedy AI + browser storage]
    Web --> Rules[Rules: board + pure transitions]
    App --> Contracts[Contracts: immutable data + ports]
    Rules --> Contracts
    Infra --> Contracts
```

Arrows are **compile-time references**, not calls to concrete implementations. At runtime DI supplies the rules engine, strategy and storage adapter to the controller. Web also references Rules to register its implementation. Application must not reference Infrastructure, Rules implementations, Blazor or JavaScript. Rules and Contracts must not reference browser APIs. Infrastructure uses `IJSRuntime` only in its browser adapter; its codec and strategy are ordinary .NET classes.

Contracts is one small shared assembly, not a fifth development stream. A owns its board/state and rules types; B owns the controller, persistence port and view types; D owns the strategy port and serialized schema. C reviews UI usability of the public facade. B coordinates changes to this assembly and solution-wide configuration. Keep ownership in a checked-in contributor table or CODEOWNERS when actual usernames are known.

| Contributor | Owned implementation | Requirements / use cases | Independent development substitute |
| --- | --- | --- | --- |
| **A — rules and model** | Hex geometry, generation, move validation, capture, score/end derivation, state validation | F01–F03 and F05–F06 calculations; UC01/02/04 | Hand-built immutable states; no UI, storage or AI needed |
| **B — application and integration** | Game controller, command admission, turn lifecycle, cancellation, view projection, DI contract coordination and integration tests | F01–F08 orchestration; UC01–UC07 | Fake rules engine, controllable strategy and in-memory save store |
| **C — Blazor presentation** | Setup/game/result screens, SVG grid, palette, owner labels, scores, errors, component tests, Web composition and static-host instructions | F01/F03/F05–F08 presentation; human interaction | Fake controller with predefined setup/human/computer/result views |
| **D — strategy and persistence** | Greedy strategy, JSON schema/codec, browser save adapter, storage errors, adapter/strategy tests | F04/F07/F08; UC03/06/07 | Fake rules engine, fixture states, in-memory/fake JavaScript storage |

B has the largest integration responsibility. Their compensation is that A owns all game calculations and D owns both AI selection and serialization. C owns the startup/deployment instructions and browser rendering checks. Each person writes tests for their own component and contributes integration cases; testing is not assigned to a separate person.

## 3. Shared data and pure rules contract

Commit real compilable definitions corresponding to the signatures below before splitting implementation work. These are the agreed API blueprint. Use `System.Collections.Immutable`; a record containing a mutable array or list does **not** satisfy the immutability contract.

```csharp
public enum GameColor { Red, Orange, Yellow, Green, Blue, Purple }
public enum PlayerKind { Human, Computer }
public enum GamePhase { Running, Finished }

public sealed record CellState(int Id, GameColor Color, int? OwnerId);
public sealed record PlayerState(
    int Id, PlayerKind Kind, int StartCellId, GameColor Color);
public sealed record NewGameOptions(ImmutableArray<PlayerKind> Players);

public sealed record GameState(
    int Width,
    int Height,
    ImmutableArray<CellState> Cells,
    ImmutableArray<PlayerState> Players,
    int CurrentPlayerIndex,
    long TurnNumber,
    GamePhase Phase);

public enum RuleError {
    None, InvalidOptions, InvalidState, FinishedGame,
    UnknownPlayer, WrongTurn, IllegalColor
}
public sealed record StateResult(GameState? State, RuleError Error);
public sealed record MoveResult(
    GameState? State, int CapturedCount, RuleError Error);
public sealed record ValidationResult(
    bool IsValid, ImmutableArray<string> Problems);
public sealed record PlayerScore(int PlayerId, int Score);
public sealed record GameSummary(
    ImmutableArray<PlayerScore> Scores,
    int NeutralCount,
    ImmutableArray<int> WinnerIds);

public interface IGameRules {
    StateResult Create(NewGameOptions options, int seed);
    ImmutableArray<GameColor> LegalColors(GameState state);
    MoveResult ApplyMove(GameState state, int playerId, GameColor color);
    ValidationResult Validate(GameState state);
    GameSummary Summarize(GameState state);
}
```

Contract semantics:

- Result success means `Error == None` and a non-null state. Failure means non-`None` error and null result state. The original state is unchanged in both cases. Expected user errors do not throw exceptions.
- `Create` returns exactly 120 cells in ascending ID order and players in turn order; `TurnNumber = 0`, `CurrentPlayerIndex = 0`. It establishes all initial territories and detects an already-full board. A fixed seed is repeatable with the pinned implementation. Invalid participant counts or all-computer options return `InvalidOptions`.
- `LegalColors` uses the fixed palette order. For a finished state it returns an empty array. Normal rule operations receive validated states; persistence uses `Validate` before handing state to them.
- `ApplyMove` checks phase, acting player and color, recolors the complete territory, and floods only neutral cells of the chosen color from **all territory boundaries**. It returns the full immutable successor and the number of newly owned cells. Ownership cannot transfer between players.
- A successful move increments `TurnNumber` once. If neutral cells remain, it sets the next player index modulo player count; otherwise it marks `Finished` and leaves the final acting player index. This pure next-state calculation belongs to Rules; Application decides when a human or computer may initiate it. Do not advance the turn in both components.
- `Summarize` returns scores in player order and winner IDs in player order. Scores are counts of owner IDs, not separately stored counters. Neutral count is the number of null owners. Winner IDs are derived only for a finished state and otherwise are empty. Application uses this summary for its view projection, so winner/score rules have one implementation. No second independently mutable representation of territory or score exists.
- Rules permits moves by either player kind. Only the application enforces whether a command came from a human or from the computer turn runner.

For `(c,r)`, always consider `(c-1,r)` and `(c+1,r)`. On even rows add `(c-1,r-1)`, `(c,r-1)`, `(c-1,r+1)`, `(c,r+1)`; on odd rows add `(c,r-1)`, `(c+1,r-1)`, `(c,r+1)`, `(c+1,r+1)`. Remove out-of-bounds positions; never wrap. Specify this in fixture tests independently of the implementation.

C uses the matching SVG geometry: for radius `s`, center `x = sqrt(3) * s * (c + 0.5 * (r % 2))`, `y = 1.5 * s * r`, plus a positive margin. Polygon vertices start at a 30-degree angle and advance by 60 degrees. Sharing this convention prevents a visually adjacent pair from being logically disconnected. UI layout code does not perform capture or determine legal moves.

`Validate` rejects invalid dimensions/cell order or count; undefined enums; duplicate/non-sequential player IDs; invalid participant configuration or start placement; missing/foreign owner IDs; invalid current index or negative turn number; duplicate player colors; missing owned start cells; owned cells whose color differs from the owner's current color; disconnected player territory; and phase inconsistent with the neutral count. It also rejects incomplete capture closure: a neutral cell adjacent to an owner's territory cannot have that owner's current color in a completed state. It need not prove that a saved state is historically reachable through a particular sequence of moves.

## 4. Controller, UI and asynchronous behavior

The controller is the **Facade** promised by the specification and the sole owner of the active session. UI components receive immutable views and submit commands. They never mutate a `GameState`, call the rules engine directly, select AI moves or write saves.

```csharp
public readonly record struct ViewToken(Guid SessionId, long Revision);
public enum Screen { Setup, Game, Result }
public sealed record PlayerView(
    int Id, PlayerKind Kind, GameColor Color, int Score);
public sealed record GameView(
    ViewToken Token,
    Screen Screen,
    GameState? State,
    bool IsBusy,
    bool CanSave,
    ImmutableArray<GameColor> LegalColors,
    ImmutableArray<PlayerView> Players,
    int NeutralCount,
    ImmutableArray<int> WinnerIds,
    CommandError BackgroundError);

public enum CommandError {
    None, InvalidOptions, NotAllowed, Busy, StaleView, IllegalColor,
    MissingSave, InvalidSave, UnsupportedSaveVersion, StorageUnavailable,
    UnexpectedFailure
}
public sealed record CommandResult(CommandError Error);

public interface IGameController {
    GameView View { get; }
    event Action? Changed;
    ValueTask<CommandResult> StartAsync(NewGameOptions options);
    ValueTask<CommandResult> ChooseColorAsync(
        ViewToken expected, GameColor color);
    ValueTask<CommandResult> SaveAsync(ViewToken expected);
    ValueTask<CommandResult> ResumeAsync();
    ValueTask<CommandResult> ReturnToSetupAsync();
}
```

`GameState` in a view is read-only board data. `Players` adds scores from the rules summary; `WinnerIds` is empty unless the game finished. The application computes `CanSave` and legal human buttons; during computer turns, busy operations or results, the view's legal colors are empty. `BackgroundError` is normally `None` and exposes a supervised computer-runner failure that has no awaiting UI command. Error enum values map to Hungarian UI messages; exception text is not a user-facing error message.

The token prevents a delayed click or double click from becoming a move on a later human turn. Increment revision on every view transition, including busy changes; issue a new session ID on start, successful resume and return to setup. Invalid commands do not increment game turn number. `StartAsync` and `ResumeAsync` are accepted only from idle setup. `ChooseColorAsync` requires a matching token and an idle running human turn. `SaveAsync` has the same token/admission rules and never changes the game state.

Commands return after their own operation commits; starting a game or choosing a color does **not** wait until every subsequent computer turn completes. The controller schedules and supervises a single computer loop:

1. Publish the current computer player and disable human actions before yielding to the browser.
2. Ask the strategy for a color using a captured immutable state.
3. Before committing, check cancellation, session identity and the captured state/revision. A result from an abandoned session cannot enter the new game.
4. Apply the move once through `IGameRules`, publish the successor, and yield between consecutive computer turns so the browser can render and process “New game.”
5. Stop on a human turn, finished game or cancellation. Never recursively launch another runner from `Changed`.

The greedy calculation is small and can be synchronous inside an asynchronous runner. Do not assume browser WebAssembly supports background threads or use `Task.Run` as a substitute for yielding. Make the yield/delay injectable in Application tests, so tests can pause a pending computer turn without real timeouts.

Use a short command admission gate: check/admit and capture state before awaiting an external operation. Do not hold that gate across strategy/storage awaits in a way that prevents return to setup. `ReturnToSetupAsync` cancels pending work and immediately publishes setup, even while the previous runner is yielding. Serialize other mutations and reject reentry. Unexpected AI failures must be observed and publish `BackgroundError = UnexpectedFailure`, with move/save input remaining disabled until reset; do not silently select an arbitrary fallback color. Reset clears the background error. Expected cancellation during reset is not a failure.

Similarly, a save/load completion for an old session must not overwrite the new session's view. A previously admitted save may have written its captured valid snapshot; returning to setup does not promise to undo that write. Never display its late success message on a different session. Dispose event subscriptions and cancellation sources. C marshals `Changed` callbacks through Blazor `InvokeAsync` and unsubscribes when a component is disposed.

## 5. Strategy and persistence contracts

```csharp
public interface IComputerStrategy {
    GameColor ChooseColor(GameState state, IGameRules rules);
}

public sealed record GameSnapshot(int Version, GameState State);
public enum StoreError {
    None, Missing, InvalidData, UnsupportedVersion, Unavailable
}
public sealed record StoreResult(StoreError Error);
public sealed record LoadResult(GameSnapshot? Snapshot, StoreError Error);

// Application-owned port; defined in Contracts for independent compilation.
public interface IGameSaveStore {
    ValueTask<StoreResult> SaveAsync(
        GameSnapshot snapshot, CancellationToken cancellationToken);
    ValueTask<LoadResult> LoadAsync(CancellationToken cancellationToken);
}
```

**Greedy AI:** receive a valid running state whose current player is a computer. Evaluate every color from `LegalColors` by calling the same `ApplyMove` used for actual moves; compare `CapturedCount`. Choose the first maximum in the palette order. Evaluation never changes the supplied state, and the strategy never commits a move or advances the controller. There is no separately implemented flood-fill for AI. An empty legal-color list in a running valid game is an invariant violation, not a pass turn.

**JSON codec:** D owns an explicit version-1 DTO representation and fixed camelCase property names. Do not serialize controller/view objects, tokens, `IsBusy`, events, dependencies, cancellation or cached scores. Store `version` and the entire state: dimensions; ordered cells with `id`, `color`, nullable `ownerId`; ordered players with `id`, `kind`, `startCellId`, `color`; `currentPlayerIndex`, `turnNumber`, `phase`. Use numeric enum IDs from section 1. Freeze a representative valid fixture and the property names in tests. Future schema changes require an explicit version decision, not silent serializer defaults.

Missing required properties must be errors, even if C# would otherwise supply a plausible default such as zero. Reject malformed JSON, null arrays, undefined enum values, unsupported versions and unreasonable input size before allocating game objects (a 256 KiB limit is ample for this fixed board). Unknown nonessential properties can be ignored for version 1. Deserialization must not run board generation or create replacement cells.

Validation has two distinct owners:

1. **D:** the store/codec checks JSON structure and version. Success returns a candidate snapshot; it does not establish game validity. On failure, `LoadResult.Snapshot` is null.
2. **B using A:** before installing the candidate, call `IGameRules.Validate`. Then enforce the save policy: a running state at a human turn. A structurally valid snapshot with inconsistent ownership, a finished state or a computer turn is an invalid save. On any failure, remain in setup with no partial state.

On save, B captures an already validated, complete state at an idle human turn and passes a version-1 snapshot. D serializes fully **before** replacing the stored value. Use one `localStorage.setItem` call, without first deleting the old save. Catch browser access/quota failures and map them to `Unavailable`; never report success after a failed write. Pass cancellation through the supported API, but do not claim it can roll back a write already performed in JavaScript.

Keep the adapter thin: codec unit tests use strings; storage tests use fake JS calls; the actual browser test verifies `setItem/getItem` survive reload at the same origin. `localStorage` contains only local game data; no server synchronization or authentication is introduced.

## 6. Individual acceptance tests

Each contributor's PR must run without the others' unfinished implementations. Test doubles live in test projects or an explicit development-only harness and are never selected by the release composition root. Agree on a small shared fixture pack, including exact expected owners, next player and scores; do not generate expected answers by calling the implementation under test.

| Owner | Minimum independently verifiable cases |
| --- | --- |
| A | All six neighbors for interior even/odd rows; corners/edges and no wrap; symmetric adjacency; valid generation for 2/3/4 players and rejection of invalid options; distinct starts/colors and initial component capture; multihop capture from different territory boundaries; opponent blocking; own/opponent color rejection without mutation; zero gain advances; wraparound turn; score sum plus neutral count is 120; win/draw and initial end detection; all state-validation invariants. |
| B | Admission in setup/human/computer/busy/finished states; stale tokens and double clicks; exactly one move/turn increment; automatic consecutive computer turns ending at a human; initially finished game; reset while AI is pending and discarded late result; stale storage completion; save only at an idle human turn; load errors remain in setup; validated load restores exactly; reset preserves save; subscribed views contain correct legal buttons/scores/winners. |
| C | Setup permits 2–4 mixed participants with at least one human; fake controller commands carry the rendered token; 120 polygons with correct odd-r placement; owners readable without relying on color alone; current player/scores/neutral count; illegal buttons disabled; computer and busy states disabled; draw/result and new-game screen; save/load error messages; unsubscribe on disposal; readable layout at 1280 × 720. |
| D | AI chooses largest **full** component rather than immediate neighbor count; palette tie order; legal choice when all gains are zero; no input-state mutation; version-1 round trip preserves every state field; missing fields/invalid JSON/size limit/unsupported versions; missing save; fake JS unavailable/quota failure; overwrite without delete; failed write retains old value; canceled operation never installs partial state. |

For small hand-built capture/AI examples, construct a 120-cell board with a controlled local pattern and irrelevant regions filled deliberately. Helpers must create internally valid ownership and player colors. For initialization edge cases that random generation rarely produces, A may use an internal deterministic color source exposed to the test assembly; it is not a production gameplay interface.

Property checks complement explicit examples: all owned territories remain monochromatic and connected; cell count stays fixed; existing opponent owners never change; a move cannot reduce territory size; and the old immutable state stays identical after simulation. Random-seed samples are useful but do not replace the explicit edge-case assertions.

## 7. Integration without continuous meetings

### Gate 0 — shared contracts and skeleton

B opens one small PR containing the solution, project references, shared types/interfaces, test project setup, pinned tooling and the fixture format. A–D review only their relevant contract semantics asynchronously. No implementation is required for approval, but the skeleton must build and each test project must execute at least one real contract/fixture assertion. Merge this before four parallel implementation branches begin.

The first fixture pack should include: a valid human turn, a valid computer turn, a multihop capture case, a zero-gain case, a two-way draw, and a version-1 JSON save. Include a pending-computer test double for B and predefined views for C. Fixtures have expected outcomes written by hand and reviewed by the relevant owners.

### Gate 1 — four independent component deliveries

All four streams can proceed after Gate 0:

- A implements Rules against state fixtures.
- B implements the controller against fake rules/strategy/storage and owns the integration harness.
- C implements the UI against the fake controller.
- D implements strategy and persistence against fake rules/JS and the JSON fixtures.

Each delivery includes its own passing tests and an example showing how to register/call it. An implementation is ready to connect when it satisfies the agreed contract, not when another person's screen works. Do not block a completed component waiting for the entire application.

### Gate 2 — early real integration

Connect real components in small PRs as they become available, in this useful order:

1. Rules + controller: start a seeded game and execute a human move through the facade.
2. Greedy strategy + controller: play human–computer and human–computer–computer setups; verify input gating and turn handoff.
3. Web + controller: render and execute those same scenarios through the real UI.
4. Store + controller + Web: save a human turn, reload the browser, resume and compare the complete board/players/next player.

These are integration dependencies, not a calendar. Connect the available components even when another component still uses a fake. Do not wait until every contributor declares their work finished before testing the first complete move.

### Gate 3 — acceptance and delivery

Use real components and the release DI configuration. Require:

- Locked restore, Release build, nonzero passing test suites for every implemented component, and a publish of the standalone WebAssembly app.
- Serve the **published** static output over HTTP in the browser suite. A development server alone does not validate deployment assets. Document any required static-host MIME settings and path/base-address configuration.
- Actual browser coverage for starting a mixed game, moving/capturing, automatic AI, illegal input, results/new game, save/reload/resume, and missing/corrupt saves. Use a test-only deterministic state seam to reach end/draw states; production must not expose an arbitrary-state import control.
- Current stable Chrome and Edge checks at the specified resolution. Headless Chromium CI is useful but does not establish that Edge was checked; record the actual browser/version used for final acceptance.
- A feature-to-test checklist covering F01–F08/UC01–UC07, class/dependency documentation and a short deployment/readme update. No acceptance item is “done” based solely on a mock test.

Once the solution exists, standard commands are `dotnet restore --locked-mode`, `dotnet build --configuration Release --no-restore`, `dotnet test --configuration Release --no-build` with the selected .NET 10 test runner, and `dotnet publish src/TerritoryCapture.Web/TerritoryCapture.Web.csproj --configuration Release --no-restore`. The skeleton PR must confirm these commands against the actual solution and record the separate browser-test command and browser installation instructions. This plan does not claim they have run before the projects exist.

## 8. Asynchronous collaboration rules

- Each PR states its requirement IDs, owned files, public-contract impact, checks actually run, and any remaining integration limitation. Small PRs with one coherent deliverable are easier to review than a whole-person final submission.
- Implementation details inside an owned component can change freely while its contract holds. A public signature, enum ID, coordinate convention or JSON schema change needs a brief issue with the proposed before/after and affected owners' written review. Submit all affected caller/fixture changes together; never silently break another branch.
- B owns `.sln`/solution-wide build settings and coordinates Contracts; C owns Web startup/DI edits. Other contributors request the required registration changes in their PR rather than independently editing these shared files.
- Keep tests and default builds green on the shared branch. Use branch/PR checks; do not merge code that works only with an uncommitted local file. Test doubles must not accidentally ship as production services.
- When blocked, post the smallest reproducible fixture, observed result, expected contract behavior and the required owner decision. Continue independent tests/UI work against the agreed fake. Weekly catchups review contract issues, completed gates and acceptance gaps; this plan imposes no dates.
- Each owner includes a short component handover: public API, invariants, how to run its tests, known limitations and the next integration step. This lets someone else finish a component if a teammate becomes unavailable.

The unavoidable shared work is the initial contract review, boundary changes and integration acceptance. The day-to-day implementations remain independent because shared state is immutable, calculations have one owner, and every dependency has a test substitute.
