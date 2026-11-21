using System.Collections.Immutable;
using TerritoryCapture.Contracts;

namespace TerritoryCapture.Rules.Tests;

public class GameRulesMoveTests
{
    private readonly GameRules _rules = new();

    private static GameState CreateState(
        int width,
        int height,
        CellState[] cells,
        int currentPlayerIndex = 0,
        long turnNumber = 0)
    {
        var players = ImmutableArray.Create(
            new PlayerState(0, PlayerKind.Human, 0, GameColor.Red),
            new PlayerState(1, PlayerKind.Human, cells.Length - 1, GameColor.Blue)
        );

        return new GameState(
            width,
            height,
            cells.ToImmutableArray(),
            players,
            currentPlayerIndex,
            turnNumber,
            GamePhase.Running
        );
    }

    [Fact]
    public void ApplyMove_CapturesConnectedNeutralCells()
    {
        var state = CreateState(
            3,
            2,
            [
                new CellState(0, GameColor.Red, 0),
                new CellState(1, GameColor.Green, null),
                new CellState(2, GameColor.Green, null),
                new CellState(3, GameColor.Yellow, null),
                new CellState(4, GameColor.Yellow, null),
                new CellState(5, GameColor.Blue, 1)
            ]);

        var result = _rules.ApplyMove(state, 0, GameColor.Green);

        Assert.Equal(RuleError.None, result.Error);
        Assert.NotNull(result.State);
        Assert.Equal(2, result.CapturedCount);

        Assert.Equal(0, result.State!.Cells[0].OwnerId);
        Assert.Equal(0, result.State.Cells[1].OwnerId);
        Assert.Equal(0, result.State.Cells[2].OwnerId);

        Assert.Equal(GameColor.Green, result.State.Players[0].Color);
    }

    [Fact]
    public void ApplyMove_CannotCaptureThroughOpponentTerritory()
    {
        var state = CreateState(
            3,
            1,
            [
                new CellState(0, GameColor.Red, 0),
                new CellState(1, GameColor.Blue, 1),
                new CellState(2, GameColor.Green, null)
            ]);

        var result = _rules.ApplyMove(state, 0, GameColor.Green);

        Assert.Equal(RuleError.None, result.Error);
        Assert.NotNull(result.State);
        Assert.Equal(0, result.CapturedCount);

        Assert.Null(result.State!.Cells[2].OwnerId);
        Assert.Equal(1, result.State.Cells[1].OwnerId);
    }

    [Fact]
    public void ApplyMove_AllowsMoveWithoutCapturingCells()
    {
        var state = CreateState(
            3,
            1,
            [
                new CellState(0, GameColor.Red, 0),
                new CellState(1, GameColor.Yellow, null),
                new CellState(2, GameColor.Blue, 1)
            ]);

        var result = _rules.ApplyMove(state, 0, GameColor.Green);

        Assert.Equal(RuleError.None, result.Error);
        Assert.NotNull(result.State);
        Assert.Equal(0, result.CapturedCount);

        Assert.Equal(GameColor.Green, result.State!.Players[0].Color);
        Assert.Equal(1, result.State.CurrentPlayerIndex);
        Assert.Equal(1, result.State.TurnNumber);
        Assert.Equal(GamePhase.Running, result.State.Phase);
    }

    [Fact]
    public void ApplyMove_DoesNotModifyOriginalState()
    {
        var state = CreateState(
            3,
            1,
            [
                new CellState(0, GameColor.Red, 0),
                new CellState(1, GameColor.Green, null),
                new CellState(2, GameColor.Blue, 1)
            ]);

        var originalCells = state.Cells.ToArray();
        var originalPlayers = state.Players.ToArray();

        var result = _rules.ApplyMove(state, 0, GameColor.Green);

        Assert.Equal(RuleError.None, result.Error);

        Assert.True(state.Cells.SequenceEqual(originalCells));
        Assert.True(state.Players.SequenceEqual(originalPlayers));
        Assert.Equal(0, state.TurnNumber);
        Assert.Equal(0, state.CurrentPlayerIndex);
    }

    [Fact]
    public void ApplyMove_FinishesGameWhenAllCellsAreCaptured()
    {
        var state = CreateState(
            3,
            1,
            [
                new CellState(0, GameColor.Red, 0),
                new CellState(1, GameColor.Green, null),
                new CellState(2, GameColor.Blue, 1)
            ]);

        var result = _rules.ApplyMove(state, 0, GameColor.Green);

        Assert.Equal(RuleError.None, result.Error);
        Assert.NotNull(result.State);
        Assert.Equal(1, result.CapturedCount);

        Assert.Equal(GamePhase.Finished, result.State!.Phase);
        Assert.Equal(1, result.State.TurnNumber);
        Assert.Equal(0, result.State.CurrentPlayerIndex);

        Assert.All(
            result.State.Cells,
            cell => Assert.NotNull(cell.OwnerId));
    }

    [Fact]
    public void ApplyMove_ReturnsInvalidStateForInvalidCurrentPlayerIndex()
    {
        var state = CreateState(
            3,
            1,
            [
                new CellState(0, GameColor.Red, 0),
                new CellState(1, GameColor.Green, null),
                new CellState(2, GameColor.Blue, 1)
            ]) with
            {
                CurrentPlayerIndex = 5
            };

        var result = _rules.ApplyMove(state, 0, GameColor.Green);

        Assert.Equal(RuleError.InvalidState, result.Error);
        Assert.Null(result.State);
        Assert.Equal(0, state.TurnNumber);
}
}
