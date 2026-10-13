using System.Text.Json;
using TerritoryCapture.Contracts;
using TerritoryCapture.TestSupport;

namespace TerritoryCapture.Infrastructure.Tests;

public class FixtureTests
{
    [Fact]
    public void AgreedJsonFixtureMatchesHumanTurnIncludingBoardAndPlayers()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "human-turn.v1.json"));
        var snapshot = JsonSerializer.Deserialize<GameSnapshot>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot.Version);
        var expected = GameFixtures.HumanTurn();
        Assert.Equal(expected.Cells.ToArray(), snapshot.State.Cells.ToArray());
        Assert.Equal(expected.Players.ToArray(), snapshot.State.Players.ToArray());
        Assert.Equal(expected.Width, snapshot.State.Width);
        Assert.Equal(expected.Height, snapshot.State.Height);
        Assert.Equal(expected.CurrentPlayerIndex, snapshot.State.CurrentPlayerIndex);
        Assert.Equal(expected.TurnNumber, snapshot.State.TurnNumber);
        Assert.Equal(expected.Phase, snapshot.State.Phase);
    }
}
