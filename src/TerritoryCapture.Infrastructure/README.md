# Infrastructure — contributor D

Implement `IComputerStrategy`, the version-1 JSON codec and `IGameSaveStore`. Add browser JS interop dependencies here when needed.

Milestone 1 adds `GreedyComputerStrategy` and `GameSnapshotCodec`. The browser
`IGameSaveStore` adapter remains future work; until it is registered, the
controller reports `StorageUnavailable` for save/resume.

Local tests: `dotnet test tests/TerritoryCapture.Infrastructure.Tests`.

Start from `tests/TerritoryCapture.TestSupport` fixtures/doubles. Extend the
strategy and codec tests as storage work proceeds.

Register the current real services in the Web composition root:

```csharp
builder.Services.AddScoped<IGameRules, GameRules>();
builder.Services.AddScoped<IComputerStrategy, GreedyComputerStrategy>();
builder.Services.AddScoped<IGameController, GameController>();
```
