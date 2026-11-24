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

    [Theory]
    [InlineData(PlayerKind.Human, PlayerKind.Human)]
    [InlineData(PlayerKind.Human, PlayerKind.Computer)]
    [InlineData(PlayerKind.Computer, PlayerKind.Human)]
    [InlineData(PlayerKind.Human, PlayerKind.Human, PlayerKind.Human)]
    [InlineData(PlayerKind.Human, PlayerKind.Human, PlayerKind.Human, PlayerKind.Human)]
    public void Create_GeneratesValidStateForPlayerConfigurations(
        params PlayerKind[] playerKinds)
    {
        var result = _rules.Create(
            new NewGameOptions(playerKinds.ToImmutableArray()),
            seed: 42);

        Assert.Equal(RuleError.None, result.Error);
        Assert.NotNull(result.State);

        var validation = _rules.Validate(result.State!);

        Assert.True(
            validation.IsValid,
            string.Join(Environment.NewLine, validation.Problems));
    }

   [Fact]
    public void Create_GeneratesSameStateForSameSeed()
    {
        var options = new NewGameOptions(
            ImmutableArray.Create(
                PlayerKind.Human,
                PlayerKind.Computer));

        var first = _rules.Create(options, seed: 42);
        var second = _rules.Create(options, seed: 42);

        Assert.Equal(RuleError.None, first.Error);
        Assert.Equal(RuleError.None, second.Error);

        Assert.NotNull(first.State);
        Assert.NotNull(second.State);

        Assert.Equal(first.State.Width, second.State.Width);
        Assert.Equal(first.State.Height, second.State.Height);
        Assert.Equal(first.State.CurrentPlayerIndex, second.State.CurrentPlayerIndex);
        Assert.Equal(first.State.TurnNumber, second.State.TurnNumber);
        Assert.Equal(first.State.Phase, second.State.Phase);

        Assert.Equal(
            first.State.Players.ToArray(),
            second.State.Players.ToArray());

        Assert.Equal(
            first.State.Cells.ToArray(),
            second.State.Cells.ToArray());
    }

    [Fact]
    public void Create_GeneratesDifferentStatesForDifferentSeeds()
    {
        var options = new NewGameOptions(
            ImmutableArray.Create(
                PlayerKind.Human,
                PlayerKind.Computer));

        var first = _rules.Create(options, seed: 42);
        var second = _rules.Create(options, seed: 123);

        Assert.Equal(RuleError.None, first.Error);
        Assert.Equal(RuleError.None, second.Error);
        Assert.False(
        first.State!.Cells.SequenceEqual(second.State!.Cells) &&
        first.State.Players.SequenceEqual(second.State.Players));
    }
}
