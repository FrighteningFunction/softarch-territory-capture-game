using TerritoryCapture.Contracts;
using TerritoryCapture.TestSupport;

namespace TerritoryCapture.Rules.Tests;

public class FixtureTests
{
    [Fact]
    public void MultiHopFixtureHasFourConnectedOrangeTargetsAndPreservesStarts()
    {
        var state = GameFixtures.MultiHop();
        Assert.Equal(120, state.Cells.Length);
        Assert.Equal(new[] { 1, 2, 13, 14 }, state.Cells.Where(c => c.Color == GameColor.Orange).Select(c => c.Id));
        Assert.All(state.Cells.Where(c => c.Color == GameColor.Orange), c => Assert.Null(c.OwnerId));
        Assert.Equal(0, state.Cells[0].OwnerId);
        Assert.Equal(1, state.Cells[119].OwnerId);
        Assert.Equal(GameColor.Red, state.Cells[0].Color);
        Assert.Equal(GameColor.Blue, state.Cells[119].Color);
    }

     [Fact]
    public void HumanTurnFixtureHasExpectedOwnersColorsAndTurn()
    {
        var state = GameFixtures.HumanTurn();

        Assert.Equal(12, state.Width);
        Assert.Equal(10, state.Height);
        Assert.Equal(120, state.Cells.Length);
        Assert.Equal(Enumerable.Range(0, 120), state.Cells.Select(c => c.Id));
        Assert.Equal(0, state.CurrentPlayerIndex);
        Assert.Equal(0, state.TurnNumber);
        Assert.Equal(GamePhase.Running, state.Phase);
        Assert.Equal(new PlayerState(0, PlayerKind.Human, 0, GameColor.Red), state.Players[0]);
        Assert.Equal(new PlayerState(1, PlayerKind.Human, 119, GameColor.Blue), state.Players[1]);
        Assert.Equal(GameColor.Red, state.Cells[0].Color);
        Assert.Equal(0, state.Cells[0].OwnerId);
        Assert.Equal(GameColor.Blue, state.Cells[119].Color);
        Assert.Equal(1, state.Cells[119].OwnerId);
        Assert.Equal(118, state.Cells.Count(c => c.OwnerId is null));
        Assert.Equal(1, state.Cells.Count(c => c.OwnerId == 0));
        Assert.Equal(1, state.Cells.Count(c => c.OwnerId == 1));
    }

    [Fact]
    public void ComputerTurnFixtureDiffersOnlyInFirstPlayerKind()
    {
        var state = GameFixtures.ComputerTurn();

        Assert.Equal(PlayerKind.Computer, state.Players[0].Kind);
        Assert.Equal(PlayerKind.Human, state.Players[1].Kind);
        Assert.Equal(0, state.CurrentPlayerIndex);
        Assert.Equal(GamePhase.Running, state.Phase);
        Assert.Equal(120, state.Cells.Length);
        Assert.Equal(118, state.Cells.Count(c => c.OwnerId is null));
    }

    [Fact]
    public void ZeroGainFixtureOffersGreenAsAnUnusedColor()
    {
        var state = GameFixtures.ZeroGain();

        Assert.Equal(GamePhase.Running, state.Phase);
        Assert.DoesNotContain(state.Cells, c => c.Color == GameColor.Green);
        Assert.Equal(118, state.Cells.Count(c => c.OwnerId is null));
        Assert.Equal(GameColor.Red, state.Players[0].Color);
        Assert.Equal(GameColor.Blue, state.Players[1].Color);
    }

    [Fact]
    public void DrawFixtureHasEqualFullTerritoriesAndTwoWinnersByHand()
    {
        var state = GameFixtures.Draw();

        Assert.Equal(GamePhase.Finished, state.Phase);
        Assert.Equal(120, state.Cells.Length);
        Assert.Equal(60, state.Cells.Count(c => c.OwnerId == 0));
        Assert.Equal(60, state.Cells.Count(c => c.OwnerId == 1));
        Assert.DoesNotContain(state.Cells, c => c.OwnerId is null);
        Assert.All(state.Cells, c =>
        Assert.Equal(state.Players[c.OwnerId!.Value].Color, c.Color));
        Assert.Equal(new[] { 0, 1 }, state.Players.Select(p => p.Id));
    }
}
