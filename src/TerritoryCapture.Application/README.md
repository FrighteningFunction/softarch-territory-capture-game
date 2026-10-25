# Application — contributor B

Implement `IGameController` using the rules, strategy and save-store interfaces. Own active state and the computer-turn lifecycle.

`GameController` owns the active session and immutable view projection. The
computer-turn yield hook is injectable for deterministic asynchronous tests.
The save-store dependency is optional until the browser adapter is available;
save and resume then return `StorageUnavailable`.

Local tests: `dotnet test tests/TerritoryCapture.Application.Tests`.

Start from `tests/TerritoryCapture.TestSupport` fixtures/doubles and extend the
controller tests for new command and lifecycle behavior.
