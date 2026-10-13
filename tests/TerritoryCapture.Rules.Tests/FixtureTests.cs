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
}
