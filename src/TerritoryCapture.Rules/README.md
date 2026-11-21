# Rules — contributor A

Implement `IGameRules`: generation, hex geometry, capture, turns, summary and validation.

The implementation project is intentionally empty; the shared API already compiles in Contracts. Add your implementation classes here. Reference Contracts only; keep dependencies behind its interfaces.

Local tests: `dotnet test tests/TerritoryCapture.Rules.Tests`.

Start from `tests/TerritoryCapture.TestSupport` fixtures/doubles. The existing tests verify starter fixtures and test support, not finished gameplay. Extend them with tests of your implementation. Supply the registration example for C in your PR.

# Milestone 0

## Hand-calculated fixture expectations

These expectations are calculated from the hand-built fixture layouts, independently of the rules implementation. Use them as expected outcomes when adding behavior tests; do not calculate expected values by calling `IGameRules`.

| Fixture | Initial state | Expected behavior / outcome |
| --- | --- | --- |
| `HumanTurn` | 12 × 10 board with cell IDs 0–119 in order. Player 0 is Human, starts at cell 0 and is Red; player 1 is Human, starts at cell 119 and is Blue. Only cells 0 and 119 are owned; the other 118 cells are neutral Yellow. Current player index 0, turn 0, phase `Running`. | Initial scores are 1/1. For a move test, choose a color other than Red and Blue. |
| `ComputerTurn` | Same board and ownership as `HumanTurn`, except player 0 is Computer and player 1 is Human. Current player index 0, phase `Running`. | Confirms the fixture can represent a computer-controlled current player; it does not itself test computer strategy. |
| `MultiHop` | Same as `HumanTurn`, with neutral Orange cells 1, 2, 13 and 14. Under the agreed odd-r hex adjacency, these cells form a connected multi-step path from cell 0. | Player 0 choosing Orange captures exactly cells 1, 2, 13 and 14. Player 0 then owns cells `{0, 1, 2, 13, 14}`; player 1 still owns cell 119. Scores are 5/1, 114 cells remain neutral, next player index is 1, turn number is 1, and phase remains `Running`. |
| `ZeroGain` | Same initial state as `HumanTurn`; Green does not appear on the board. | Green is legal because it differs from both players' current colors. Choosing Green captures 0 cells; scores remain 1/1, 118 cells remain neutral, next player index is 1, turn number is 1, and phase remains `Running`. |
| `Draw` | The left six columns belong to player 0 (Red), and the right six columns belong to player 1 (Blue). Each territory contains 60 cells; no cells are neutral. | The state is `Finished`, scores are 60/60, and winner IDs are `[0, 1]`. |
