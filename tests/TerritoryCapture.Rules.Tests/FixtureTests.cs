using System.Collections.Immutable;
using TerritoryCapture.Contracts;
using TerritoryCapture.Rules;
using TerritoryCapture.TestSupport;

namespace TerritoryCapture.Rules.Tests;

public class FixtureTests
{
    [Fact]
    public void MultiHopFixtureHasFourConnectedOrangeTargetsAndPreservesStarts()
    {
        var state = GameFixtures.MultiHop();
        Assert.Equal(120, state.Cells.Length);
        Assert.Equal(new[] { 1, 2, 13, 14 },
            state.Cells.Where(cell => cell.Color == GameColor.Orange).Select(cell => cell.Id));
        Assert.All(state.Cells.Where(cell => cell.Color == GameColor.Orange),
            cell => Assert.Null(cell.OwnerId));
        Assert.Equal(0, state.Cells[0].OwnerId);
        Assert.Equal(1, state.Cells[119].OwnerId);
        Assert.Equal(GameColor.Red, state.Cells[0].Color);
        Assert.Equal(GameColor.Blue, state.Cells[119].Color);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void CreateBuildsAValidDeterministicGame(int playerCount)
    {
        var rules = new GameRules();
        var options = new NewGameOptions(
            Enumerable.Range(0, playerCount)
                .Select(index => index == 1 ? PlayerKind.Computer : PlayerKind.Human)
                .ToImmutableArray());

        var first = rules.Create(options, 1234);
        var second = rules.Create(options, 1234);

        Assert.Equal(RuleError.None, first.Error);
        Assert.NotNull(first.State);
        Assert.Equal(first.State!.Cells.ToArray(), second.State!.Cells.ToArray());
        Assert.Equal(first.State.Players.ToArray(), second.State.Players.ToArray());
        Assert.True(rules.Validate(first.State).IsValid);
        Assert.Equal(120, first.State.Cells.Length);
        Assert.Equal(Enumerable.Range(0, 120), first.State.Cells.Select(cell => cell.Id));
        Assert.Equal(0, first.State.TurnNumber);
        Assert.Equal(0, first.State.CurrentPlayerIndex);
        Assert.Equal(playerCount, first.State.Players.Select(player => player.Color).Distinct().Count());
        Assert.Equal(playerCount, first.State.Players.Select(player => player.StartCellId).Distinct().Count());
    }

    [Fact]
    public void CreateRejectsInvalidParticipantConfigurations()
    {
        var rules = new GameRules();
        Assert.Equal(RuleError.InvalidOptions,
            rules.Create(new NewGameOptions([PlayerKind.Human]), 1).Error);
        Assert.Equal(RuleError.InvalidOptions,
            rules.Create(new NewGameOptions([PlayerKind.Computer, PlayerKind.Computer]), 1).Error);
    }

    [Fact]
    public void NeighborGeometryMatchesTheContractOnEvenAndOddRows()
    {
        Assert.Equal(new[] { 40, 41, 52, 54, 64, 65 },
            GameRules.Neighbors(53, 12, 10).Order());
        Assert.Equal(new[] { 53, 54, 64, 66, 77, 78 },
            GameRules.Neighbors(65, 12, 10).Order());
    }

    [Fact]
    public void DoesNotWrapAcrossBoardEdges()
    {
        var rules = new GameRules();
        var state = new GameState(12, 10,
            Enumerable.Range(0, 120).Select(id => id switch
            {
                0 => new CellState(id, GameColor.Red, 0),
                11 => new CellState(id, GameColor.Orange, null),
                119 => new CellState(id, GameColor.Blue, 1),
                _ => new CellState(id, GameColor.Yellow, null)
            }).ToImmutableArray(),
            [new PlayerState(0, PlayerKind.Human, 0, GameColor.Red),
             new PlayerState(1, PlayerKind.Human, 119, GameColor.Blue)],
            0, 0, GamePhase.Running);

        var result = rules.ApplyMove(state, 0, GameColor.Orange);

        Assert.Equal(RuleError.None, result.Error);
        Assert.Equal(0, result.CapturedCount);
        Assert.Null(result.State!.Cells[11].OwnerId);
    }

    [Fact]
    public void CapturesMultihopComponentsFromMultipleTerritoryBoundariesWithoutMutatingInput()
    {
        var rules = new GameRules();
        var state = GameFixtures.MultiHop();
        var originalCells = state.Cells.ToArray();

        var result = rules.ApplyMove(state, 0, GameColor.Orange);

        Assert.Equal(RuleError.None, result.Error);
        Assert.Equal(4, result.CapturedCount);
        Assert.All(new[] { 1, 2, 13, 14 },
            id => Assert.Equal(0, result.State!.Cells[id].OwnerId));
        Assert.Equal(GameColor.Orange, result.State!.Players[0].Color);
        Assert.Equal(1, result.State!.CurrentPlayerIndex);
        Assert.Equal(originalCells, state.Cells.ToArray());
        Assert.Equal(0, state.TurnNumber);
    }

    [Fact]
    public void OpponentTerritoryBlocksCaptureAndNeverChangesOwner()
    {
        var rules = new GameRules();
        var cells = Enumerable.Range(0, 120)
            .Select(id => new CellState(id, GameColor.Yellow, null))
            .ToArray();
        cells[0] = new CellState(0, GameColor.Red, 0);
        cells[119] = new CellState(119, GameColor.Blue, 1);
        foreach (var id in Enumerable.Range(1, 9).Select(row => row * 12)
                     .Concat(Enumerable.Range(1, 11).Select(column => 9 * 12 + column)))
        {
            cells[id] = new CellState(id, GameColor.Blue, 1);
        }
        cells[25] = cells[25] with { Color = GameColor.Orange };
        var state = new GameState(12, 10, cells.ToImmutableArray(),
            [new PlayerState(0, PlayerKind.Human, 0, GameColor.Red),
             new PlayerState(1, PlayerKind.Human, 119, GameColor.Blue)],
            0, 0, GamePhase.Running);

        var validation = rules.Validate(state);
        Assert.True(validation.IsValid, string.Join("; ", validation.Problems));
        var result = rules.ApplyMove(state, 0, GameColor.Orange);

        Assert.Equal(0, result.CapturedCount);
        Assert.Equal(1, result.State!.Cells[12].OwnerId);
        Assert.Null(result.State.Cells[25].OwnerId);
    }

    [Fact]
    public void ZeroGainMoveAdvancesTurnAndSummaryConservesBoardCount()
    {
        var rules = new GameRules();
        var state = GameFixtures.ZeroGain();
        var result = rules.ApplyMove(state, 0, GameColor.Green);
        var summary = rules.Summarize(result.State!);

        Assert.Equal(0, result.CapturedCount);
        Assert.Equal(1, result.State!.TurnNumber);
        Assert.Equal(1, result.State.CurrentPlayerIndex);
        Assert.Equal(120, summary.Scores.Sum(score => score.Score) + summary.NeutralCount);
        Assert.Empty(summary.WinnerIds);
    }

    [Fact]
    public void CapturingTheLastNeutralFinishesWithTheFinalPlayerAsWinner()
    {
        var rules = new GameRules();
        var cells = Enumerable.Range(0, 120)
            .Select(id => id == 119
                ? new CellState(id, GameColor.Blue, 1)
                : id == 0
                    ? new CellState(id, GameColor.Red, 0)
                    : new CellState(id, GameColor.Orange, null))
            .ToImmutableArray();
        var state = new GameState(12, 10, cells,
            [new PlayerState(0, PlayerKind.Human, 0, GameColor.Red),
             new PlayerState(1, PlayerKind.Human, 119, GameColor.Blue)],
            0, 0, GamePhase.Running);

        var result = rules.ApplyMove(state, 0, GameColor.Orange);
        var summary = rules.Summarize(result.State!);

        Assert.Equal(118, result.CapturedCount);
        Assert.Equal(GamePhase.Finished, result.State!.Phase);
        Assert.Equal(0, result.State.CurrentPlayerIndex);
        Assert.Equal(new[] { 0 }, summary.WinnerIds);
        Assert.Equal(119, summary.Scores[0].Score);
        Assert.Equal(1, summary.Scores[1].Score);
    }

    [Fact]
    public void FinishedDrawHasEveryMaximumScorerAsWinner()
    {
        var rules = new GameRules();
        var summary = rules.Summarize(GameFixtures.Draw());

        Assert.Equal(new[] { 0, 1 }, summary.WinnerIds);
        Assert.Equal(60, summary.Scores[0].Score);
        Assert.Equal(60, summary.Scores[1].Score);
        Assert.Equal(0, summary.NeutralCount);
    }

    [Fact]
    public void MoveRejectsUnknownWrongTurnAndEveryPlayersCurrentColorWithoutMutation()
    {
        var rules = new GameRules();
        var state = GameFixtures.HumanTurn();
        var original = state.Cells.ToArray();

        Assert.Equal(RuleError.UnknownPlayer,
            rules.ApplyMove(state, 99, GameColor.Orange).Error);
        Assert.Equal(RuleError.WrongTurn,
            rules.ApplyMove(state, 1, GameColor.Orange).Error);
        Assert.Equal(RuleError.IllegalColor,
            rules.ApplyMove(state, 0, GameColor.Red).Error);
        Assert.Equal(RuleError.IllegalColor,
            rules.ApplyMove(state, 0, GameColor.Blue).Error);
        Assert.Equal(original, state.Cells.ToArray());
    }

    [Fact]
    public void TurnWrapsAndFinishedStateProducesSingleWinner()
    {
        var rules = new GameRules();
        var secondPlayerTurn = GameFixtures.HumanTurn() with
        {
            CurrentPlayerIndex = 1,
            TurnNumber = 1
        };
        var move = rules.ApplyMove(secondPlayerTurn, 1, GameColor.Orange);
        var winnerBoard = Enumerable.Range(0, 120)
            .Select(id => id % 12 < 7
                ? new CellState(id, GameColor.Red, 0)
                : new CellState(id, GameColor.Blue, 1))
            .ToImmutableArray();
        var winnerState = GameFixtures.Draw() with
        {
            Cells = winnerBoard,
            Phase = GamePhase.Finished
        };

        Assert.Equal(RuleError.None, move.Error);
        Assert.Equal(0, move.State!.CurrentPlayerIndex);
        Assert.Equal(2, move.State.TurnNumber);
        Assert.Equal(new[] { 0 }, rules.Summarize(winnerState).WinnerIds);
    }

    [Fact]
    public void ValidationRejectsInvalidOwnerAndDisconnectedTerritory()
    {
        var rules = new GameRules();
        var state = GameFixtures.HumanTurn();
        var invalidOwner = state with
        {
            Cells = state.Cells.SetItem(1, state.Cells[1] with { OwnerId = 99 })
        };
        var disconnected = state with
        {
            Cells = state.Cells.SetItem(60, state.Cells[60] with
            {
                OwnerId = 0,
                Color = GameColor.Red
            })
        };

        Assert.False(rules.Validate(invalidOwner).IsValid);
        Assert.False(rules.Validate(disconnected).IsValid);
    }

}
