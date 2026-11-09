using System.Collections.Immutable;
using TerritoryCapture.Contracts;

namespace TerritoryCapture.Rules.Tests;

public class GameRulesSummaryTests
{
    private readonly GameRules _rules = new();

    private static GameState CreateState(
        CellState[] cells,
        GamePhase phase)
    {
        var players = ImmutableArray.Create(
            new PlayerState(0, PlayerKind.Human, 0, GameColor.Red),
            new PlayerState(1, PlayerKind.Human, cells.Length - 1, GameColor.Blue)
        );

        return new GameState(
            cells.Length,
            1,
            cells.ToImmutableArray(),
            players,
            0,
            0,
            phase);
    }

    [Fact]
    public void Summarize_ReturnsCorrectScoresAndNeutralCount()
    {
        var state = CreateState(
            [
                new CellState(0, GameColor.Red, 0),
                new CellState(1, GameColor.Red, 0),
                new CellState(2, GameColor.Green, null),
                new CellState(3, GameColor.Blue, 1),
                new CellState(4, GameColor.Blue, 1)
            ],
            GamePhase.Running);

        var result = _rules.Summarize(state);

        Assert.Equal(
            new[]
            {
                new PlayerScore(0, 2),
                new PlayerScore(1, 2)
            },
            result.Scores);

        Assert.Equal(1, result.NeutralCount);
        Assert.Empty(result.WinnerIds);
    }

    [Fact]
    public void Summarize_ReturnsWinnerWhenGameIsFinished()
    {
        var state = CreateState(
            [
                new CellState(0, GameColor.Red, 0),
                new CellState(1, GameColor.Red, 0),
                new CellState(2, GameColor.Blue, 1),
                new CellState(3, GameColor.Blue, 1),
                new CellState(4, GameColor.Blue, 1)
            ],
            GamePhase.Finished);

        var result = _rules.Summarize(state);

        Assert.Equal(2, result.Scores[0].Score);
        Assert.Equal(3, result.Scores[1].Score);
        Assert.Equal(0, result.NeutralCount);

        Assert.Equal(new[] { 1 }, result.WinnerIds);
    }

    [Fact]
    public void Summarize_ReturnsAllPlayersWhenThereIsATie()
    {
        var state = CreateState(
            [
                new CellState(0, GameColor.Red, 0),
                new CellState(1, GameColor.Red, 0),
                new CellState(2, GameColor.Blue, 1),
                new CellState(3, GameColor.Blue, 1)
            ],
            GamePhase.Finished);

        var result = _rules.Summarize(state);

        Assert.Equal(
            new[] { 0, 1 },
            result.WinnerIds);
    }

    [Fact]
    public void Summarize_DoesNotModifyOriginalState()
    {
        var state = CreateState(
            [
                new CellState(0, GameColor.Red, 0),
                new CellState(1, GameColor.Green, null),
                new CellState(2, GameColor.Blue, 1)
            ],
            GamePhase.Running);

        var originalCells = state.Cells.ToArray();
        var originalPlayers = state.Players.ToArray();

        _rules.Summarize(state);

        Assert.True(state.Cells.SequenceEqual(originalCells));
        Assert.True(state.Players.SequenceEqual(originalPlayers));
    }
}
