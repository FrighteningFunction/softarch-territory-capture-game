# Rules — contributor A

`GameRules` implements `IGameRules`: generation, hex geometry, capture, turns,
summary and validation.

The pure rules project references Contracts only. It has no UI, persistence or
strategy dependencies.

Local tests: `dotnet test tests/TerritoryCapture.Rules.Tests`.

Start from `tests/TerritoryCapture.TestSupport` fixtures/doubles and extend the
focused rules tests with changes to the rules contract.
