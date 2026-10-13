using System.Collections.Immutable;
using TerritoryCapture.Contracts;

namespace TerritoryCapture.TestSupport;

// Hand-built completed states; expectations do not call a rules implementation.
public static class GameFixtures
{
    public static GameState HumanTurn() => Create(PlayerKind.Human);
    public static GameState ComputerTurn() => Create(PlayerKind.Computer);
    public static GameState MultiHop() => HumanTurn() with
    {
        Cells = HumanTurn().Cells.Select(cell => cell.Id is 1 or 2 or 13 or 14
            ? cell with { Color = GameColor.Orange } : cell).ToImmutableArray()
    };
    // Choosing Green on HumanTurn has zero gain; it is a legal color.
    public static GameState ZeroGain() => HumanTurn();
    public static GameState Draw() => HumanTurn() with
    {
        Cells = Enumerable.Range(0, 120).Select(id => new CellState(id,
            id % 12 < 6 ? GameColor.Red : GameColor.Blue,
            id % 12 < 6 ? 0 : 1)).ToImmutableArray(),
        TurnNumber = 20,
        Phase = GamePhase.Finished
    };

    private static GameState Create(PlayerKind firstKind) => new(
        12, 10,
        Enumerable.Range(0, 120).Select(id => id switch
        {
            0 => new CellState(id, GameColor.Red, 0),
            119 => new CellState(id, GameColor.Blue, 1),
            _ => new CellState(id, GameColor.Yellow, null)
        }).ToImmutableArray(),
        [new PlayerState(0, firstKind, 0, GameColor.Red),
         new PlayerState(1, PlayerKind.Human, 119, GameColor.Blue)],
        0, 0, GamePhase.Running);
}
