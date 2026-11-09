using TerritoryCapture.Contracts;
using System.Collections.Immutable;

namespace TerritoryCapture.Rules.Tests;

public class GameRulesGenerationTests
{
    private readonly GameRules _rules = new();

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void CreateProducesValidBoard(int playerCount)
    {
        var options = new NewGameOptions(
            Enumerable.Repeat(PlayerKind.Human, playerCount)
                .ToImmutableArray());

        var result = _rules.Create(options, 42);

        Assert.Equal(RuleError.None, result.Error);
        Assert.NotNull(result.State);

        var state = result.State!;

        Assert.Equal(12, state.Width);
        Assert.Equal(10, state.Height);
        Assert.Equal(120, state.Cells.Length);
        Assert.Equal(Enumerable.Range(0, 120), state.Cells.Select(c => c.Id));

        Assert.Equal(playerCount, state.Players.Length);
        Assert.Equal(0, state.CurrentPlayerIndex);
        Assert.Equal(0, state.TurnNumber);

        Assert.All(state.Players, player =>
        {
            Assert.Equal(player.Id, player.StartCellId switch
            {
                0 => 0,
                11 => 1,
                108 => 2,
                119 => playerCount - 1,
                _ => -1
            });
        });

        Assert.Equal(
            playerCount,
            state.Players.Select(p => p.Color).Distinct().Count());

        Assert.All(state.Players, player =>
        {
            var startCell = state.Cells[player.StartCellId];

            Assert.Equal(player.Id, startCell.OwnerId);
            Assert.Equal(player.Color, startCell.Color);
        });
    }

    [Fact]
    public void CreateIsDeterministicForSameSeed()
    {
        var options = new NewGameOptions(
        [
            PlayerKind.Human,
            PlayerKind.Human
        ]);

        var first = _rules.Create(options, 1234).State!;
        var second = _rules.Create(options, 1234).State!;

        Assert.Equal(first.Width, second.Width);
        Assert.Equal(first.Height, second.Height);
        Assert.Equal(first.CurrentPlayerIndex, second.CurrentPlayerIndex);
        Assert.Equal(first.TurnNumber, second.TurnNumber);
        Assert.Equal(first.Phase, second.Phase);

        Assert.True(first.Cells.SequenceEqual(second.Cells));
        Assert.True(first.Players.SequenceEqual(second.Players));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void CreateRejectsInvalidPlayerCount(int playerCount)
    {
        var options = new NewGameOptions(
            Enumerable.Repeat(PlayerKind.Human, playerCount)
                .ToImmutableArray());

        var result = _rules.Create(options, 42);

        Assert.Null(result.State);
        Assert.Equal(RuleError.InvalidOptions, result.Error);
    }

    [Fact]
    public void CreateRejectsAllComputerPlayers()
    {
        var options = new NewGameOptions(
        [
            PlayerKind.Computer,
            PlayerKind.Computer
        ]);

        var result = _rules.Create(options, 42);

        Assert.Null(result.State);
        Assert.Equal(RuleError.InvalidOptions, result.Error);
    }
}
