# Infrastructure — contributor D

## Delivery scope

D's strategy, codec, browser save adapter, service registrations and independent .NET/browser tests are implemented. The production Web shell still awaits A/B/C's gameplay implementations. See [browser checks and integration handoff](../../tests/browser/README.md).

Implemented:

- `GreedyComputerStrategy : IComputerStrategy`: simulate each legal color using `IGameRules.ApplyMove`, select greatest full capture, and break ties by palette ID. A zero-gain move is still selected. Simulations all receive the original immutable state; the strategy never installs the returned successors. An invalid turn or inconsistent rules result is reported rather than hidden by a fallback.
- `GameSnapshotJsonCodec`: encode/decode the agreed version-1 JSON format, using generated serializer metadata for trimmed Blazor builds. It matches `tests/TerritoryCapture.TestSupport/Fixtures/human-turn.v1.json` without changing shared contracts or fixtures.

- `BrowserGameSaveStore : IGameSaveStore`: one atomic localStorage value through real `IJSRuntime`, typed errors and cancellation. No Rules/Application implementation dependency.
- `AddTerritoryCaptureInfrastructure`: idempotent registration for C's composition root.

The published harness verifies D's real services with test-only fixture transitions/rules doubles. Three full-application smoke tests are ready for integration, pending real A/B/C implementations and C's documented DOM hooks. These integration tests are not claimed as passed.

## Run locally

Install the SDK matching the root `global.json`, then run from the repository root:

```sh
dotnet restore TerritoryCapture.sln --locked-mode
dotnet test tests/TerritoryCapture.Infrastructure.Tests --configuration Release --no-restore
```

The infrastructure suite contains 45 cases, including the existing fixture test. Strategy tests use a rules double with independent, hand-written gains. They verify selection and delegation, not A's unimplemented flood-fill algorithm. Codec tests cover the shared fixture, complete round trips, required fields, numeric enums, malformed input, version rejection, nulls and the UTF-8 size limit. Adapter tests cover overwrite, failure preservation, typed errors, cancellation and real DI resolution. Seven published-harness browser tests cover actual interop and reload. Full-solution checks are documented in the root README; Debug tests also exercise the existing fake UI.

## Registration handoff for C

When real services are connected, register the stateless implementations in the Web composition root:

```csharp
using TerritoryCapture.Contracts;
using TerritoryCapture.Infrastructure;

builder.Services.AddTerritoryCaptureInfrastructure();
```

The Blazor host supplies `IJSRuntime`. This registers a singleton codec/strategy and scoped `IGameSaveStore` implemented by `BrowserGameSaveStore`. B injects `IGameSaveStore` and `IComputerStrategy` into the controller. Keep development fakes out of release startup. BrowserHarness demonstrates real DI without changing C's production composition.

## Codec contract and schema

```csharp
var codec = new GameSnapshotJsonCodec();
string json = codec.Encode(new GameSnapshot(GameSnapshotJsonCodec.CurrentVersion, state));
LoadResult candidate = codec.Decode(json);
```

`Encode` accepts a version-1 snapshot with initialized collections and defined enums, and throws `ArgumentException` for unsupported or structurally invalid input. It serializes fully before any storage write. `Decode` returns:

| Input | Result |
| --- | --- |
| `null` (missing storage value) | `Missing`, no snapshot |
| Integer version other than 1 | `UnsupportedVersion`, no snapshot |
| Bad JSON, missing required fields, null collections/elements, wrong types or undefined enums | `InvalidData`, no snapshot |
| More than 256 KiB of UTF-8 data | `InvalidData`, no snapshot |
| Structurally valid version 1 | `None`, candidate snapshot |

Wire shape is `{ "version": 1, "state": { ... } }`. State properties are `width`, `height`, `cells`, `players`, `currentPlayerIndex`, `turnNumber`, `phase`. Cells contain `id`, `color`, `ownerId`; players contain `id`, `kind`, `startCellId`, `color`. Every field is required, including nullable `ownerId`. Enums are numeric; names are case-sensitive camelCase. Unknown additional properties are ignored. The adapter uses `GameSnapshotJsonCodec.StorageKey` (`territory-capture.save.v1`). Tokens, cached scores and UI state are not serialized.

**B must call `IGameRules.Validate` and enforce the running-human-turn save policy before installing a decoded candidate.** Codec success only establishes the wire shape. It deliberately does not duplicate board dimensions, ownership, connectivity, capture closure or phase validation. A computer-turn or finished fixture can round-trip through the codec without being eligible for resume.

## Storage and controller lifecycle handoff

Encoding finishes before one `localStorage.setItem`; the old value is never deleted first. Quota/security/JS-runtime failures map to `Unavailable`; structurally invalid encode input maps to `InvalidData`. Load returns codec errors without installing anything in the controller. Failed load/new game never delete a previous save.

Cancellation propagates as `OperationCanceledException`, including cancellation after an awaited JS call; it cannot undo an already committed JS write. B must ignore old-session completions and display success/failure only for the current session. B owns command admission and uses A's validator before installing a candidate. Store success does not establish game validity.

No database, credentials, backend or custom JavaScript module is required. See the browser README for commands, schema evidence and the remaining real-application acceptance gate.

## Verification — November 16, 2026

Verified from a clean local clone of this branch, without pre-existing build outputs:

- Locked solution and BrowserHarness restores succeeded; lockfiles remained unchanged.
- Release solution build: zero warnings/errors.
- Release and Debug solution tests: 50 passed in each configuration, including 45 Infrastructure cases; none failed or skipped. Existing Rules/Application starter tests do not establish real gameplay.
- Release publication of both the production Web shell and the separate trimmed BrowserHarness succeeded.
- `npm ci` and the seven infrastructure browser checks passed against the clean published harness using Debian Chromium 154.0.8037.57.
- The three application smoke tests were discovered with `--list`, but were not run as acceptance: A/B/C's real implementations and C's proposed DOM hooks are not present on this branch/main. Actual Chrome/Edge game acceptance remains pending integration.
