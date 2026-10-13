using TerritoryCapture.Contracts;
using TerritoryCapture.TestSupport;

namespace TerritoryCapture.Integration.Tests;

// Baseline fixture checks only. B adds real component integration as they arrive.
public class FixtureContractTests
{
    [Fact]
    public void DrawFixtureHasTwoEqualTerritoriesAndNoNeutralCells()
    {
        var state = GameFixtures.Draw();
        Assert.Equal(GamePhase.Finished, state.Phase);
        Assert.Equal(60, state.Cells.Count(c => c.OwnerId == 0));
        Assert.Equal(60, state.Cells.Count(c => c.OwnerId == 1));
        Assert.DoesNotContain(state.Cells, c => c.OwnerId is null);
        Assert.All(state.Cells, c => Assert.Equal(state.Players[c.OwnerId!.Value].Color, c.Color));
    }
}
