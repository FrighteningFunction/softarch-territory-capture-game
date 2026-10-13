# Shared test support

A owns game fixtures; B owns doubles; D owns the JSON fixture. This project is referenced only by tests.

| Fixture | Hand-written expectation |
| --- | --- |
| HumanTurn | Player 0 human, Red at cell 0; player 1 human, Blue at 119; 118 neutral Yellow cells; scores 1/1. |
| ComputerTurn | Same board, player 0 computer and player 1 human. |
| MultiHop | HumanTurn with neutral Orange cells 1, 2, 13, 14. Player 0 choosing Orange gains exactly 4; scores 5/1; 114 neutral; next player 1; turn 1. |
| ZeroGain | HumanTurn; Green gains 0; scores 1/1; 118 neutral; next player 1; turn 1. |
| Draw | Left six columns belong to player 0, right six to player 1; scores 60/60, zero neutral; Finished, winners 0 and 1. |
| Fixtures/human-turn.v1.json | Version 1 representation of HumanTurn, numeric enum IDs and camelCase properties. |

`ScriptedRules` returns configured results; `FixedStrategy` returns a configured color; `MemorySaveStore` retains a snapshot and observes cancellation. They do not implement real game behavior. B can add a controllable async yield when implementing the computer loop.
