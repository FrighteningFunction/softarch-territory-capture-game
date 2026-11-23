using System.Collections.Immutable;
using TerritoryCapture.Contracts;

namespace TerritoryCapture.Rules.Tests;

public class GameRulesSummaryTests
{
    private readonly GameRules _rules = new();

   private static GameState CreateState(
    int player0Score,
    int player1Score,
    GamePhase phase)
    {
        const int totalCells = 120;

        if (player0Score < 1 ||
            player1Score < 1 ||
            player0Score + player1Score > totalCells)
        {
            throw new ArgumentOutOfRangeException(
                nameof(player0Score),
                "Invalid score configuration.");
        }

        var neutralCount = totalCells - player0Score - player1Score;

        if ((phase == GamePhase.Running && neutralCount == 0) ||
            (phase == GamePhase.Finished && neutralCount > 0))
        {
            throw new ArgumentException(
                "Game phase must match the neutral cell count.");
        }

        var cells = Enumerable.Range(0, totalCells)
            .Select(id =>
            {
                if (id < player0Score)
                {
                    return new CellState(id, GameColor.Red, 0);
                }

                if (id >= totalCells - player1Score)
                {
                    return new CellState(id, GameColor.Blue, 1);
                }

                return new CellState(id, GameColor.Green, null);
            })
            .ToImmutableArray();

        var players = ImmutableArray.Create(
            new PlayerState(0, PlayerKind.Human, 0, GameColor.Red),
            new PlayerState(1, PlayerKind.Human, 119, GameColor.Blue));

        return new GameState(
            12,
            10,
            cells,
            players,
            0,
            0,
            phase);
    }

    [Fact]
    public void Summarize_ReturnsCorrectScoresAndNeutralCount()
    {
        var state = CreateState(2, 2, GamePhase.Running);

        var result = _rules.Summarize(state);

        Assert.Equal(
            new[]
            {
                new PlayerScore(0, 2),
                new PlayerScore(1, 2)
            },
            result.Scores);

        Assert.Equal(116, result.NeutralCount);
        Assert.Empty(result.WinnerIds);
    }

    [Fact]
    public void Summarize_ReturnsWinnerWhenGameIsFinished()
    {
        var state = CreateState(2, 118, GamePhase.Finished);

        var result = _rules.Summarize(state);

        Assert.Equal(2, result.Scores[0].Score);
        Assert.Equal(118, result.Scores[1].Score);
        Assert.Equal(0, result.NeutralCount);

        Assert.Equal(new[] { 1 }, result.WinnerIds);
    }

    [Fact]
    public void Summarize_ReturnsAllPlayersWhenThereIsATie()
    {
        var state = CreateState(60, 60, GamePhase.Finished);

        var result = _rules.Summarize(state);

        Assert.Equal(
            new[] { 0, 1 },
            result.WinnerIds);
    }

    [Fact]
    public void Summarize_DoesNotModifyOriginalState()
    {
        var state = CreateState(1, 1, GamePhase.Running);

        var originalCells = state.Cells.ToArray();
        var originalPlayers = state.Players.ToArray();

        _rules.Summarize(state);

        Assert.True(state.Cells.SequenceEqual(originalCells));
        Assert.True(state.Players.SequenceEqual(originalPlayers));
    }
}
