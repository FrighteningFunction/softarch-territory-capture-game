# Rules - contributor A

## Delivery scope

A's `GameRules : IGameRules` implementation is complete. It provides the application's pure game rules and board model behavior, covering F01, F02, F03, F05 and F06, and supporting UC01, UC02 and UC04.

Implemented:

- `GameRules : IGameRules`: stateless implementation of the shared game rules contract.
- `Create`: generates a fixed 12x10 board containing 120 cells and supporting 2–4 participants, requiring at least one Human player. Starting positions follow the agreed corner order. Player colors are selected without replacement from the six-color palette, and board colors are generated using the supplied seed. Each player's initial connected component matching their starting color is assigned to them.
- `GetNeighbors`: calculates edge-sharing neighbors using odd-r offset coordinates. Board boundaries are respected without wrapping.
- `LegalColors`: returns colors not currently used by any participant, in the fixed palette order. Finished games and invalid states do not provide legal colors.
- `ApplyMove`: validates the state, acting player, turn order and selected color. Recolors the acting player's entire territory and captures all reachable neutral cells matching the selected color. Opponent-owned cells and neutral cells of other colors cannot be captured or traversed. Every successful move increments `TurnNumber` and advances to the next participant when neutral cells remain. If no neutral cells remain, the game enters `Finished` and the final acting player remains the current player. A zero-gain move is valid and still advances the turn.
- `Summarize`: calculates player scores from owned cells and the number of neutral cells. Winner IDs are returned only for finished games. All participants tied for the highest score are winners.
- `Validate`: checks board dimensions, collections, identifiers, participant configuration, colors, ownership, starting positions, territory connectivity, capture closure and phase consistency.

The Rules layer does not decide whether a command originates from a human or computer player. It permits a move for either player kind when the supplied player is the current player. Application owns command admission and computer-turn orchestration.

## Run locally

Install the SDK matching the root `global.json`, then run from the repository root:

```sh
dotnet restore TerritoryCapture.sln --locked-mode
dotnet test tests/TerritoryCapture.Rules.Tests --configuration Release --no-restore
```

To run the full solution test suite:

```sh
dotnet test TerritoryCapture.sln --configuration Release --no-restore
```

## Test coverage

The independent Rules test suite is located in `tests/TerritoryCapture.Rules.Tests`.

The suite covers:

- Shared hand-calculated fixtures for human turns, computer turns, multi-hop capture, zero-gain moves and draws.
- Game generation for valid participant configurations and rejection of invalid options.
- Seeded generation and initial territory assignment.
- Even-row and odd-row hex neighbors, board corners and edges, and no wrapping.
- Legal color calculation and finished-game behavior.
- Connected neutral capture, opponent blocking, zero-gain moves and immutable input states.
- Game summaries, score calculation, neutral counts, winner selection and draws.
- State validation, including dimensions, player and cell metadata, ownership, starting cells, connectivity, capture closure and phase consistency.

The hand-calculated fixtures provide independent expected outcomes for core gameplay behavior. Expected values in behavior tests are calculated from fixture layouts rather than by calling the implementation under test.

## Rules and state invariants

The board is fixed at 12 columns by 10 rows, containing exactly 120 cells. Cell IDs are zero-based and calculated as `row * 12 + column`.

The board uses pointy-top hexagons with odd rows shifted right (odd-r offset coordinates). Two cells are neighbors only when they share an edge; corner contact does not count. Board edges do not wrap.

The fixed palette and its order are:

| ID | Color |
| --- | --- |
| 0 | Red |
| 1 | Orange |
| 2 | Yellow |
| 3 | Green |
| 4 | Blue |
| 5 | Purple |

Starting positions follow the agreed corner order:

| Player count | Starting cell IDs |
| --- | --- |
| 2 | 0, 119 |
| 3 | 0, 11, 119 |
| 4 | 0, 11, 108, 119 |

Player IDs correspond to their positions in the participant list and never change. The same list order determines turn order.

A player may select any palette color that is not currently used by any participant, including their own current color being excluded. A successful move recolors the player's complete territory and captures the entire connected component of neutral cells matching the selected color that is reachable from that territory.

Opponent-owned cells are barriers: they cannot be captured or traversed. Neutral cells of other colors are also not traversed. A legal color choice that captures no additional cells remains a successful move and does not end the game.

A game finishes when no neutral cells remain, including when this condition is reached during initialization. The score of a player is the number of cells whose `OwnerId` equals that player's ID. All participants with the maximum score win; multiple winners represent a draw.

### State validation

`Validate` checks the consistency of a complete `GameState` before `ApplyMove` proceeds.

The validation includes:

- Fixed board dimensions and exact cell count.
- Initialized cell and player collections, with no null entries.
- Valid participant count and at least one Human player.
- Sequential player IDs matching their positions in the participant collection.
- Defined player kinds, colors, cell colors and game phase.
- Unique player colors.
- Valid starting cell IDs and predefined starting positions.
- Cell IDs matching their positions in the cell collection.
- Valid owner references for all owned cells.
- Consistency between an owned cell's color and its owner's color.
- Ownership of every player's starting cell.
- Connectivity of every player's territory from their starting cell.
- Capture closure: a neutral cell matching a player's current color cannot remain directly adjacent to that player's territory.
- Valid current player index and non-negative turn number.
- Phase consistency with the number of neutral cells.

Invalid states are returned as a `ValidationResult` with explanatory problems. `ApplyMove` rejects an invalid state with `RuleError.InvalidState` before applying a move.

Validation checks structural and gameplay consistency, not whether a state can be reconstructed from a particular historical sequence of moves.

## Territory capture algorithm

Initial territory assignment and move execution use breadth-first search (BFS) over the odd-r hexagonal grid.

During initialization, the search starts at each player's starting cell and assigns the connected component of cells matching that player's starting color. Previously assigned cells are not traversed, so initial territories cannot overlap.

During a move:

1. All cells owned by the acting player are recolored to the selected color.
2. All cells in the player's existing territory are added to the BFS queue as starting points.
3. The algorithm examines each starting cell's hexagonal neighbors.
4. A neutral neighbor matching the selected color is assigned to the acting player and added to the queue.
5. The search continues from newly captured cells, allowing complete multi-step connected components to be captured.
6. Opponent-owned cells and neutral cells of other colors are not traversed.

A visited array prevents processing a cell more than once. The algorithm returns the number of newly captured cells as `CapturedCount`.

## Project structure

- `GameRules.cs`: main `GameRules` partial class implementing `IGameRules`.
- `GameRules.Generation.cs`: board dimensions, palette, starting positions, seeded generation, initial territory assignment and hex-neighbor calculation.
- `GameRules.Moves.cs`: legal color calculation and move execution.
- `GameRules.Summary.cs`: player scores, neutral cell count and winner determination.
- `GameRules.Validation.cs`: structural and gameplay invariant validation.
- `AssemblyInfo.cs`: exposes internal members to the Rules test assembly.

## Dependencies and registration handoff

The Rules project references `TerritoryCapture.Contracts` only. It has no dependency on Application, Infrastructure, Web, Blazor or browser APIs.

Register the implementation in the Web composition root:

```csharp
using TerritoryCapture.Contracts;
using TerritoryCapture.Rules;

builder.Services.AddScoped<IGameRules, GameRules>();
```

`GameRules` is stateless and can be registered as a scoped service.

Application consumes `IGameRules` through Contracts. The controller delegates game creation, move execution, legal color calculation, validation and summary calculation to this implementation. It must not duplicate territory ownership, capture or scoring logic.

## Verification — November 24, 2026

- Locked Rules test project restore succeeded; lockfiles remained unchanged.
- Release and Debug Rules builds succeeded with zero warnings/errors.
- Release and Debug Rules tests: 58 passed in each configuration; none failed or skipped.