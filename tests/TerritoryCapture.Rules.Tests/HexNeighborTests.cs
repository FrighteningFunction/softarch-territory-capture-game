
using TerritoryCapture.Rules;

namespace TerritoryCapture.Rules.Tests;

public class HexNeighborTests
{
    [Fact]
    public void EvenRowHasExpectedNeighbors()
    {
        // Cell 25: column 1, row 2 (even).
        var neighbors = GameRules.GetNeighbors(25, 12, 10);

        Assert.Equal(
            new[] { 24, 26, 12, 13, 36, 37 },
            neighbors);
    }

    [Fact]
    public void OddRowHasExpectedNeighbors()
    {
        // Cell 13: column 1, row 1 (odd).
        var neighbors = GameRules.GetNeighbors(13, 12, 10);

        Assert.Equal(
            new[] { 12, 14, 1, 2, 25, 26 },
            neighbors);
    }

    [Fact]
    public void TopLeftCornerHasOnlyTwoNeighbors()
    {
        var neighbors = GameRules.GetNeighbors(0, 12, 10);

        Assert.Equal(new[] { 1, 12 }, neighbors);
    }

    [Fact]
    public void BottomRightCornerHasOnlyTwoNeighbors()
    {
        var neighbors = GameRules.GetNeighbors(119, 12, 10);

        Assert.Equal(new[] { 118, 107 }, neighbors);
    }

    [Fact]
    public void NeighborsNeverWrapAcrossBoardEdges()
    {
        var neighbors = GameRules.GetNeighbors(11, 12, 10);

        Assert.DoesNotContain(12, neighbors);
        Assert.DoesNotContain(0, neighbors);
        Assert.All(neighbors, id => Assert.InRange(id, 0, 119));
    }
}
