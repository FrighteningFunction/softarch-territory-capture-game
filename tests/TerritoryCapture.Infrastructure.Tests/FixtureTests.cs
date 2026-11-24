using System.Text.Json;
using TerritoryCapture.Contracts;
using TerritoryCapture.Infrastructure;
using TerritoryCapture.Rules;
using TerritoryCapture.TestSupport;

namespace TerritoryCapture.Infrastructure.Tests;

public class FixtureTests
{
    [Fact]
    public void AgreedJsonFixtureMatchesHumanTurnIncludingBoardAndPlayers()
    {
        var json = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "human-turn.v1.json"));
        var snapshot = GameSnapshotCodec.Deserialize(json);

        Assert.Equal(StoreError.None, snapshot.Error);
        Assert.NotNull(snapshot.Snapshot);
        Assert.Equal(1, snapshot.Snapshot.Version);
        var expected = GameFixtures.HumanTurn();
        Assert.Equal(expected.Cells.ToArray(), snapshot.Snapshot.State.Cells.ToArray());
        Assert.Equal(expected.Players.ToArray(), snapshot.Snapshot.State.Players.ToArray());
        Assert.Equal(expected.Width, snapshot.Snapshot.State.Width);
        Assert.Equal(expected.Height, snapshot.Snapshot.State.Height);
        Assert.Equal(expected.CurrentPlayerIndex, snapshot.Snapshot.State.CurrentPlayerIndex);
        Assert.Equal(expected.TurnNumber, snapshot.Snapshot.State.TurnNumber);
        Assert.Equal(expected.Phase, snapshot.Snapshot.State.Phase);
    }

    [Fact]
    public void VersionOneRoundTripPreservesEveryGameStateFieldAndUsesNumericEnums()
    {
        var state = GameFixtures.HumanTurn();
        var json = GameSnapshotCodec.Serialize(new GameSnapshot(1, state));
        using var document = JsonDocument.Parse(json);
        var decoded = GameSnapshotCodec.Deserialize(json);
        var stateJson = document.RootElement.GetProperty("state");

        Assert.Equal(JsonValueKind.Number,
            stateJson.GetProperty("cells")[0].GetProperty("color").ValueKind);
        Assert.Equal(JsonValueKind.Null,
            stateJson.GetProperty("cells")[1].GetProperty("ownerId").ValueKind);
        Assert.Equal(StoreError.None, decoded.Error);
        Assert.NotNull(decoded.Snapshot);
        Assert.Equal(state.Cells.ToArray(), decoded.Snapshot.State.Cells.ToArray());
        Assert.Equal(state.Players.ToArray(), decoded.Snapshot.State.Players.ToArray());
        Assert.Equal(state.Width, decoded.Snapshot.State.Width);
        Assert.Equal(state.Height, decoded.Snapshot.State.Height);
        Assert.Equal(state.CurrentPlayerIndex, decoded.Snapshot.State.CurrentPlayerIndex);
        Assert.Equal(state.TurnNumber, decoded.Snapshot.State.TurnNumber);
        Assert.Equal(state.Phase, decoded.Snapshot.State.Phase);
    }

    [Fact]
    public void RejectsMissingRequiredFieldsMalformedJsonAndOversizedInput()
    {
        var json = GameSnapshotCodec.Serialize(
            new GameSnapshot(1, GameFixtures.HumanTurn()));
        using var parsed = JsonDocument.Parse(json);
        var missingTurnNumber = json.Replace("\"turnNumber\":0,", string.Empty,
            StringComparison.Ordinal);

        Assert.Equal(StoreError.InvalidData,
            GameSnapshotCodec.Deserialize(missingTurnNumber).Error);
        Assert.Equal(StoreError.InvalidData,
            GameSnapshotCodec.Deserialize("{").Error);
        Assert.Equal(StoreError.InvalidData,
            GameSnapshotCodec.Deserialize(new string(' ', GameSnapshotCodec.MaximumJsonBytes + 1)).Error);
        Assert.Equal(StoreError.Missing, GameSnapshotCodec.Deserialize(null).Error);
    }

    [Fact]
    public void ReportsUnsupportedVersionAndDoesNotConstructFallbackState()
    {
        var json = GameSnapshotCodec.Serialize(
            new GameSnapshot(1, GameFixtures.HumanTurn()));
        var unsupported = json.Replace("\"version\":1", "\"version\":2",
            StringComparison.Ordinal);

        var result = GameSnapshotCodec.Deserialize(unsupported);

        Assert.Equal(StoreError.UnsupportedVersion, result.Error);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public void RejectsUndefinedEnumValuesAsInvalidData()
    {
        var json = GameSnapshotCodec.Serialize(
            new GameSnapshot(1, GameFixtures.HumanTurn()));
        var invalidColor = json.Replace("\"color\":0", "\"color\":99",
            StringComparison.Ordinal);

        Assert.Equal(StoreError.InvalidData,
            GameSnapshotCodec.Deserialize(invalidColor).Error);
    }

    [Fact]
    public void GreedyStrategyUsesFullRulesGainAndDoesNotMutateInput()
    {
        var rules = new GameRules();
        var state = GameFixtures.ComputerTurn();
        var originalCells = state.Cells.ToArray();
        var expected = rules.LegalColors(state)
            .Select(color => (Color: color,
                Gain: rules.ApplyMove(state, 0, color).CapturedCount))
            .OrderByDescending(move => move.Gain)
            .ThenBy(move => (int)move.Color)
            .First().Color;

        var selected = new GreedyComputerStrategy().ChooseColor(state, rules);

        Assert.Equal(expected, selected);
        Assert.Equal(originalCells, state.Cells.ToArray());
    }

    [Fact]
    public void GreedyStrategyUsesPaletteOrderForTiesAndCanChooseZeroGainColor()
    {
        var state = GameFixtures.ComputerTurn();
        var rules = new ScriptedRules
        {
            Colors = [GameColor.Orange, GameColor.Green, GameColor.Purple],
            Move = new MoveResult(state, 0, RuleError.None)
        };

        var selected = new GreedyComputerStrategy().ChooseColor(state, rules);

        Assert.Equal(GameColor.Orange, selected);
        Assert.Equal(3, rules.ApplyCalls);
    }
}
