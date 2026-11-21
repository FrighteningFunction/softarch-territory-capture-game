using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TerritoryCapture.Contracts;

namespace TerritoryCapture.Infrastructure;

/// <summary>
/// Version-1 wire format only. The controller must validate game invariants with
/// IGameRules.Validate and enforce the human-turn save policy before installing a load.
/// </summary>
public sealed class GameSnapshotJsonCodec
{
    public const int CurrentVersion = 1;
    public const string StorageKey = "territory-capture.save.v1";
    public const int MaxUtf8Bytes = 256 * 1024;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        NumberHandling = JsonNumberHandling.Strict,
        TypeInfoResolver = GameSnapshotJsonContext.Default
    };

    public string Encode(GameSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Version != CurrentVersion || snapshot.State is null ||
            snapshot.State.Cells.IsDefault || snapshot.State.Players.IsDefault ||
            snapshot.State.Cells.Any(cell => cell is null || !Enum.IsDefined(cell.Color)) ||
            snapshot.State.Players.Any(player => player is null || !Enum.IsDefined(player.Color) ||
                !Enum.IsDefined(player.Kind)) || !Enum.IsDefined(snapshot.State.Phase))
        {
            throw new ArgumentException("Snapshot does not match the version-1 schema.", nameof(snapshot));
        }

        var state = snapshot.State;
        var data = new SnapshotData
        {
            Version = CurrentVersion,
            State = new StateData
            {
                Width = state.Width, Height = state.Height,
                CurrentPlayerIndex = state.CurrentPlayerIndex,
                TurnNumber = state.TurnNumber, Phase = state.Phase,
                Cells = state.Cells.Select(cell => new CellData
                {
                    Id = cell.Id, Color = cell.Color, OwnerId = cell.OwnerId
                }).ToArray(),
                Players = state.Players.Select(player => new PlayerData
                {
                    Id = player.Id, Kind = player.Kind,
                    StartCellId = player.StartCellId, Color = player.Color
                }).ToArray()
            }
        };
        var json = JsonSerializer.Serialize(data, Options);
        if (Encoding.UTF8.GetByteCount(json) > MaxUtf8Bytes)
            throw new ArgumentException("Snapshot exceeds the save size limit.", nameof(snapshot));
        return json;
    }

    public LoadResult Decode(string? json)
    {
        if (json is null)
            return new(null, StoreError.Missing);
        if (Encoding.UTF8.GetByteCount(json) > MaxUtf8Bytes)
            return new(null, StoreError.InvalidData);

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("version", out var version) ||
                version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number))
                return new(null, StoreError.InvalidData);
            if (number != CurrentVersion)
                return new(null, StoreError.UnsupportedVersion);

            var data = root.Deserialize<SnapshotData>(Options);
            var state = data?.State;
            if (state is null || state.Cells is null || state.Players is null ||
                !Enum.IsDefined(state.Phase) ||
                state.Cells.Any(cell => cell is null || !Enum.IsDefined(cell.Color)) ||
                state.Players.Any(player => player is null || !Enum.IsDefined(player.Color) ||
                    !Enum.IsDefined(player.Kind)))
                return new(null, StoreError.InvalidData);

            var game = new GameState(state.Width, state.Height,
                state.Cells.Select(cell => new CellState(cell.Id, cell.Color, cell.OwnerId)).ToImmutableArray(),
                state.Players.Select(player => new PlayerState(
                    player.Id, player.Kind, player.StartCellId, player.Color)).ToImmutableArray(),
                state.CurrentPlayerIndex, state.TurnNumber, state.Phase);
            return new(new GameSnapshot(CurrentVersion, game), StoreError.None);
        }
        catch (JsonException)
        {
            return new(null, StoreError.InvalidData);
        }
    }

    // Explicit required wire fields prevent omitted zero-valued IDs/enums from
    // silently becoming plausible game data. Keep DTOs separate from domain records.
    internal sealed class SnapshotData
    {
        public SnapshotData() { }
        public required int Version { get; init; }
        public required StateData State { get; init; }
    }

    internal sealed class StateData
    {
        public StateData() { }
        public required int Width { get; init; }
        public required int Height { get; init; }
        public required CellData[] Cells { get; init; }
        public required PlayerData[] Players { get; init; }
        public required int CurrentPlayerIndex { get; init; }
        public required long TurnNumber { get; init; }
        public required GamePhase Phase { get; init; }
    }

    internal sealed class CellData
    {
        public CellData() { }
        public required int Id { get; init; }
        public required GameColor Color { get; init; }
        public required int? OwnerId { get; init; }
    }

    internal sealed class PlayerData
    {
        public PlayerData() { }
        public required int Id { get; init; }
        public required PlayerKind Kind { get; init; }
        public required int StartCellId { get; init; }
        public required GameColor Color { get; init; }
    }
}

// Blazor's trimmed publish may disable serializer reflection. Generate metadata
// for the wire DTOs rather than relying on reflection over domain records.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GameSnapshotJsonCodec.SnapshotData))]
internal partial class GameSnapshotJsonContext : JsonSerializerContext
{
}
