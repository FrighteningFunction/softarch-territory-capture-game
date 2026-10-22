# Infrastructure — contributor D

## Delivery scope

The Milestone 0 skeleton, pinned SDK, locked dependencies, shared test-support project and version-1 JSON fixture already exist. This delivery verifies that foundation and adds D's standalone Milestone 1 strategy and codec. It does not connect gameplay to the Web shell.

Implemented:

- `GreedyComputerStrategy : IComputerStrategy`: simulate each legal color using `IGameRules.ApplyMove`, select greatest full capture, and break ties by palette ID. A zero-gain move is still selected. Simulations all receive the original immutable state; the strategy never installs the returned successors. An invalid turn or inconsistent rules result is reported rather than hidden by a fallback.
- `GameSnapshotJsonCodec`: encode/decode the agreed version-1 JSON format, using generated serializer metadata for trimmed Blazor builds. It matches `tests/TerritoryCapture.TestSupport/Fixtures/human-turn.v1.json` without changing shared contracts or fixtures.

Still scheduled for Milestone 2: `IGameSaveStore` browser adapter, storage error mapping, and Playwright checks with the integrated published app. No browser interop dependency is needed yet. Rules and controller implementation remain A's and B's responsibilities.

## Run locally

Install the SDK matching the root `global.json`, then run from the repository root:

```sh
dotnet restore TerritoryCapture.sln --locked-mode
dotnet test tests/TerritoryCapture.Infrastructure.Tests --configuration Release --no-restore
```

The infrastructure suite contains 36 passing cases, including the existing fixture test. Strategy tests use a rules double with independent, hand-written gains. They verify selection and delegation, not A's unimplemented flood-fill algorithm. Codec tests cover the shared fixture, complete round trips, required fields, numeric enums, malformed input, version rejection, nulls and the UTF-8 size limit. Full-solution checks are documented in the root README; Debug tests also exercise the existing fake UI.

## Registration handoff for C

When real services are connected, register the stateless implementations in the Web composition root:

```csharp
using TerritoryCapture.Contracts;
using TerritoryCapture.Infrastructure;

builder.Services.AddSingleton<IComputerStrategy, GreedyComputerStrategy>();
builder.Services.AddSingleton<GameSnapshotJsonCodec>();
```

No registration for `IGameSaveStore` is supplied yet. The codec is injected into the future browser adapter; the controller continues to depend on the save-store port, not the codec. Keep development fakes out of release startup.

## Codec contract and schema

```csharp
var codec = new GameSnapshotJsonCodec();
string json = codec.Encode(new GameSnapshot(GameSnapshotJsonCodec.CurrentVersion, state));
LoadResult candidate = codec.Decode(json);
```

`Encode` accepts a version-1 snapshot with initialized collections and defined enums, and throws `ArgumentException` for unsupported or structurally invalid input. It serializes fully before any future storage write. `Decode` returns:

| Input | Result |
| --- | --- |
| `null` (missing storage value) | `Missing`, no snapshot |
| Integer version other than 1 | `UnsupportedVersion`, no snapshot |
| Bad JSON, missing required fields, null collections/elements, wrong types or undefined enums | `InvalidData`, no snapshot |
| More than 256 KiB of UTF-8 data | `InvalidData`, no snapshot |
| Structurally valid version 1 | `None`, candidate snapshot |

Wire shape is `{ "version": 1, "state": { ... } }`. State properties are `width`, `height`, `cells`, `players`, `currentPlayerIndex`, `turnNumber`, `phase`. Cells contain `id`, `color`, `ownerId`; players contain `id`, `kind`, `startCellId`, `color`. Every field is required, including nullable `ownerId`. Enums are numeric; names are case-sensitive camelCase. Unknown additional properties are ignored. The future adapter uses `GameSnapshotJsonCodec.StorageKey` (`territory-capture.save.v1`). Tokens, cached scores and UI state are not serialized.

**B must call `IGameRules.Validate` and enforce the running-human-turn save policy before installing a decoded candidate.** Codec success only establishes the wire shape. It deliberately does not duplicate board dimensions, ownership, connectivity, capture closure or phase validation. A computer-turn or finished fixture can round-trip through the codec without being eligible for resume.
