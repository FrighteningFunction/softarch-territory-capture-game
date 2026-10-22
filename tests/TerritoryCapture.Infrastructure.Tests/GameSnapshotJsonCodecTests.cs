using System.Collections.Immutable;
using System.Text.Json.Nodes;
using TerritoryCapture.Contracts;
using TerritoryCapture.TestSupport;

namespace TerritoryCapture.Infrastructure.Tests;

public class GameSnapshotJsonCodecTests
{
    private readonly GameSnapshotJsonCodec _codec = new();

    private static string FixtureJson => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "human-turn.v1.json"));

    [Fact]
    public void EncodedHumanTurnMatchesAgreedWireFixture()
    {
        var json = _codec.Encode(new(1, GameFixtures.HumanTurn()));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(FixtureJson), JsonNode.Parse(json)));
    }

    [Theory]
    [InlineData("human")]
    [InlineData("computer")]
    [InlineData("multihop")]
    [InlineData("draw")]
    public void RoundTripPreservesAllStateFields(string fixture)
    {
        var state = fixture switch
        {
            "computer" => GameFixtures.ComputerTurn(),
            "multihop" => GameFixtures.MultiHop(),
            "draw" => GameFixtures.Draw(),
            _ => GameFixtures.HumanTurn()
        };
        // Exercise nondefault turn/index values as well as the original fixtures.
        state = state with { TurnNumber = 17, CurrentPlayerIndex = 1 };
        var result = _codec.Decode(_codec.Encode(new(1, state)));
        Assert.Equal(StoreError.None, result.Error);
        var snapshot = Assert.IsType<GameSnapshot>(result.Snapshot);
        Assert.Equal(1, snapshot.Version);
        var actual = snapshot.State;
        Assert.Equal(state.Width, actual.Width);
        Assert.Equal(state.Height, actual.Height);
        Assert.Equal(state.CurrentPlayerIndex, actual.CurrentPlayerIndex);
        Assert.Equal(state.TurnNumber, actual.TurnNumber);
        Assert.Equal(state.Phase, actual.Phase);
        Assert.Equal(state.Cells.ToArray(), actual.Cells.ToArray());
        Assert.Equal(state.Players.ToArray(), actual.Players.ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void UnsupportedVersionIsDistinctFromMalformedData(int version)
    {
        AssertError($$"""{"version":{{version}}}""", StoreError.UnsupportedVersion);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"version\":\"1\"}")]
    [InlineData("{\"version\":1.5}")]
    [InlineData("{\"version\":1,\"state\":null}")]
    public void MalformedDataNeverReturnsAPartialSnapshot(string json) =>
        AssertError(json, StoreError.InvalidData);

    [Fact]
    public void NullMeansTheSaveIsMissing() => AssertError(null, StoreError.Missing);

    [Fact]
    public void EveryWirePropertyIsRequiredIncludingNullableOwnerAndZeroValuedFields()
    {
        var groups = new[]
        {
            (Path: "root", Fields: new[] { "version", "state" }),
            (Path: "state", Fields: new[] { "width", "height", "cells", "players", "currentPlayerIndex", "turnNumber", "phase" }),
            (Path: "cell", Fields: new[] { "id", "color", "ownerId" }),
            (Path: "player", Fields: new[] { "id", "kind", "startCellId", "color" })
        };
        foreach (var group in groups)
        foreach (var field in group.Fields)
        {
            var root = JsonNode.Parse(FixtureJson)!;
            var target = group.Path switch
            {
                "state" => root["state"]!,
                "cell" => root["state"]!["cells"]![0]!,
                "player" => root["state"]!["players"]![0]!,
                _ => root
            };
            Assert.True(target.AsObject().Remove(field));
            AssertError(root.ToJsonString(), StoreError.InvalidData);
        }
    }

    [Theory]
    [InlineData("cells")]
    [InlineData("players")]
    public void NullCollectionsAndNullElementsAreInvalid(string collection)
    {
        var root = JsonNode.Parse(FixtureJson)!;
        root["state"]![collection]![0] = null;
        AssertError(root.ToJsonString(), StoreError.InvalidData);
        root["state"]![collection] = null;
        AssertError(root.ToJsonString(), StoreError.InvalidData);
    }

    [Theory]
    [InlineData("cell", "color")]
    [InlineData("player", "color")]
    [InlineData("player", "kind")]
    [InlineData("state", "phase")]
    public void UndefinedEnumsAreInvalid(string targetName, string field)
    {
        var root = JsonNode.Parse(FixtureJson)!;
        var state = root["state"]!;
        var target = targetName switch
        {
            "cell" => state["cells"]![0]!,
            "player" => state["players"]![0]!,
            _ => state
        };
        target[field] = 99;
        AssertError(root.ToJsonString(), StoreError.InvalidData);
    }

    [Fact]
    public void NumericFieldsMustNotBeStrings()
    {
        var root = JsonNode.Parse(FixtureJson)!;
        root["state"]!["cells"]![0]!["color"] = "0";
        AssertError(root.ToJsonString(), StoreError.InvalidData);
    }

    [Fact]
    public void LimitCountsUtf8BytesRatherThanCharacters()
    {
        var root = JsonNode.Parse(FixtureJson)!;
        root["note"] = new string('€', 90_000);
        var json = root.ToJsonString(new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        Assert.True(json.Length < GameSnapshotJsonCodec.MaxUtf8Bytes);
        AssertError(json, StoreError.InvalidData);
    }

    [Fact]
    public void ExtraPropertiesAreIgnoredButDomainValidationRemainsTheControllersJob()
    {
        var root = JsonNode.Parse(FixtureJson)!;
        root["note"] = "ignored";
        root["state"]!["cells"]![0]!["ownerId"] = 99;
        var result = _codec.Decode(root.ToJsonString());
        Assert.Equal(StoreError.None, result.Error);
        Assert.Equal(99, result.Snapshot!.State.Cells[0].OwnerId);
    }

    [Fact]
    public void EncoderRejectsUnsupportedVersionAndUninitializedCollections()
    {
        Assert.Throws<ArgumentException>(() => _codec.Encode(new(2, GameFixtures.HumanTurn())));
        Assert.Throws<ArgumentException>(() => _codec.Encode(new(1,
            GameFixtures.HumanTurn() with { Cells = default(ImmutableArray<CellState>) })));
    }

    private void AssertError(string? json, StoreError expected)
    {
        var result = _codec.Decode(json);
        Assert.Equal(expected, result.Error);
        Assert.Null(result.Snapshot);
    }
}
