using System.Collections.Immutable;
using TerritoryCapture.Contracts;
using TerritoryCapture.TestSupport;

namespace TerritoryCapture.Infrastructure.Tests;

public class GreedyComputerStrategyTests
{
    private readonly GreedyComputerStrategy _strategy = new();

    [Fact]
    public void ChoosesGreatestSimulatedFullCaptureAndDoesNotCommitAnySuccessor()
    {
        var state = GameFixtures.ComputerTurn();
        var cellsBefore = state.Cells.ToArray();
        var playersBefore = state.Players.ToArray();
        var rules = new GainRules(new()
        {
            [GameColor.Orange] = 4, // Full multihop gain returned by Rules, not neighbor count.
            [GameColor.Yellow] = 3,
            [GameColor.Green] = 0,
            [GameColor.Purple] = 1
        });

        Assert.Equal(GameColor.Orange, _strategy.ChooseColor(state, rules));

        Assert.Equal(4, rules.Calls.Count);
        Assert.All(rules.Calls, call =>
        {
            Assert.Same(state, call.State);
            Assert.Equal(0, call.PlayerId);
        });
        Assert.Equal(cellsBefore, state.Cells.ToArray());
        Assert.Equal(playersBefore, state.Players.ToArray());
        Assert.Equal(0, state.TurnNumber);
        Assert.Equal(0, state.CurrentPlayerIndex);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void TiesIncludingAllZeroUsePaletteOrderEvenWhenDependencyOrderDiffers(int gain)
    {
        var rules = new GainRules(new()
        {
            [GameColor.Purple] = gain,
            [GameColor.Green] = gain,
            [GameColor.Orange] = gain
        });
        Assert.Equal(GameColor.Orange, _strategy.ChooseColor(GameFixtures.ComputerTurn(), rules));
        Assert.Equal(new[] { GameColor.Orange, GameColor.Green, GameColor.Purple },
            rules.Calls.Select(call => call.Color).ToArray());
    }

    [Fact]
    public void UsesCurrentPlayerRatherThanAssumingPlayerZero()
    {
        var state = GameFixtures.HumanTurn();
        state = state with
        {
            CurrentPlayerIndex = 1,
            Players = state.Players.SetItem(1, state.Players[1] with { Kind = PlayerKind.Computer })
        };
        var rules = new GainRules(new() { [GameColor.Green] = 0 });
        Assert.Equal(GameColor.Green, _strategy.ChooseColor(state, rules));
        Assert.Equal(1, Assert.Single(rules.Calls).PlayerId);
    }

    [Fact]
    public void HumanOrFinishedTurnIsNotEvaluated()
    {
        var rules = new GainRules(new() { [GameColor.Green] = 0 });
        Assert.Throws<ArgumentException>(() => _strategy.ChooseColor(GameFixtures.HumanTurn(), rules));
        Assert.Throws<ArgumentException>(() => _strategy.ChooseColor(GameFixtures.Draw(), rules));
        Assert.Empty(rules.Calls);
    }

    [Fact]
    public void MissingLegalColorsIsAnInvariantFailureRatherThanPassingTheTurn()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _strategy.ChooseColor(GameFixtures.ComputerTurn(), new GainRules(new())));
    }

    [Fact]
    public void RejectedSimulationIsReportedRatherThanChoosingAnArbitraryFallback()
    {
        var rules = new GainRules(new() { [GameColor.Green] = 0 }) { RejectMove = true };
        Assert.Throws<InvalidOperationException>(() =>
            _strategy.ChooseColor(GameFixtures.ComputerTurn(), rules));
    }

    // Only supplies simulation outcomes; deliberately contains no capture algorithm.
    private sealed class GainRules(Dictionary<GameColor, int> gains) : IGameRules
    {
        public List<(GameState State, int PlayerId, GameColor Color)> Calls { get; } = [];
        public bool RejectMove { get; init; }
        public ImmutableArray<GameColor> LegalColors(GameState state) => gains.Keys.ToImmutableArray();
        public MoveResult ApplyMove(GameState state, int playerId, GameColor color)
        {
            Calls.Add((state, playerId, color));
            return RejectMove ? new(null, 0, RuleError.IllegalColor) :
                new(state with { TurnNumber = state.TurnNumber + 1 }, gains[color], RuleError.None);
        }
        public StateResult Create(NewGameOptions options, int seed) => throw new NotSupportedException();
        public ValidationResult Validate(GameState state) => throw new NotSupportedException();
        public GameSummary Summarize(GameState state) => throw new NotSupportedException();
    }
}
