using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TerritoryCapture.Contracts;

namespace TerritoryCapture.Infrastructure;

public static class GameSnapshotCodec
{
    public const int CurrentVersion = 1;
    public const int MaximumJsonBytes = 256 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        MaxDepth = 16
    };

    public static string Serialize(GameSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Version != CurrentVersion)
        {
            throw new ArgumentException("Only version-1 snapshots can be serialized.", nameof(snapshot));
        }
        ArgumentNullException.ThrowIfNull(snapshot.State);

        var dto = SnapshotDto.FromSnapshot(snapshot);
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        if (Encoding.UTF8.GetByteCount(json) > MaximumJsonBytes)
        {
            throw new InvalidOperationException("Serialized game snapshot exceeds the size limit.");
        }

        return json;
    }

    public static LoadResult Deserialize(string? json)
    {
        if (json is null)
        {
            return new LoadResult(null, StoreError.Missing);
        }
        if (Encoding.UTF8.GetByteCount(json) > MaximumJsonBytes)
        {
            return new LoadResult(null, StoreError.InvalidData);
        }

        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                MaxDepth = 16,
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
            if (!HasRequiredProperties(document.RootElement,
                    "version", "state")
                || !TryGetState(document.RootElement, out var stateElement))
            {
                return new LoadResult(null, StoreError.InvalidData);
            }

            if (!HasRequiredProperties(stateElement,
                    "width", "height", "cells", "players", "currentPlayerIndex",
                    "turnNumber", "phase")
                || !HasRequiredArrayItems(stateElement, "cells", "id", "color", "ownerId")
                || !HasRequiredArrayItems(stateElement, "players",
                    "id", "kind", "startCellId", "color"))
            {
                return new LoadResult(null, StoreError.InvalidData);
            }

            if (!document.RootElement.GetProperty("version").TryGetInt32(out var version))
            {
                return new LoadResult(null, StoreError.InvalidData);
            }
            if (version != CurrentVersion)
            {
                return new LoadResult(null, StoreError.UnsupportedVersion);
            }

            var dto = JsonSerializer.Deserialize<SnapshotDto>(document.RootElement.GetRawText(), JsonOptions);
            if (dto is null || dto.State is null || dto.State.Cells is null || dto.State.Players is null)
            {
                return new LoadResult(null, StoreError.InvalidData);
            }
            if (!Enum.IsDefined(dto.State.Phase)
                || dto.State.Cells.Any(cell => !Enum.IsDefined(cell.Color))
                || dto.State.Players.Any(player =>
                    !Enum.IsDefined(player.Kind) || !Enum.IsDefined(player.Color)))
            {
                return new LoadResult(null, StoreError.InvalidData);
            }

            return new LoadResult(dto.ToSnapshot(), StoreError.None);
        }
        catch (JsonException)
        {
            return new LoadResult(null, StoreError.InvalidData);
        }
        catch (NotSupportedException)
        {
            return new LoadResult(null, StoreError.InvalidData);
        }
        catch (OverflowException)
        {
            return new LoadResult(null, StoreError.InvalidData);
        }
    }

    private static bool TryGetState(JsonElement root, out JsonElement state)
    {
        state = default;
        return root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("state", out state)
            && state.ValueKind == JsonValueKind.Object;
    }

    private static bool HasRequiredProperties(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out _))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasRequiredArrayItems(
        JsonElement parent, string arrayName, params string[] requiredProperties)
    {
        if (!parent.TryGetProperty(arrayName, out var array)
            || array.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var item in array.EnumerateArray())
        {
            if (!HasRequiredProperties(item, requiredProperties))
            {
                return false;
            }
        }

        return true;
    }

    private sealed class SnapshotDto
    {
        public int Version { get; set; }
        public StateDto? State { get; set; }

        public static SnapshotDto FromSnapshot(GameSnapshot snapshot) => new()
        {
            Version = snapshot.Version,
            State = StateDto.FromState(snapshot.State)
        };

        public GameSnapshot ToSnapshot() => new(Version, State!.ToState());
    }

    private sealed class StateDto
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public CellDto[]? Cells { get; set; }
        public PlayerDto[]? Players { get; set; }
        public int CurrentPlayerIndex { get; set; }
        public long TurnNumber { get; set; }
        public GamePhase Phase { get; set; }

        public static StateDto FromState(GameState state) => new()
        {
            Width = state.Width,
            Height = state.Height,
            Cells = state.Cells.Select(CellDto.FromCell).ToArray(),
            Players = state.Players.Select(PlayerDto.FromPlayer).ToArray(),
            CurrentPlayerIndex = state.CurrentPlayerIndex,
            TurnNumber = state.TurnNumber,
            Phase = state.Phase
        };

        public GameState ToState() => new(
            Width,
            Height,
            Cells!.Select(cell => cell.ToCell()).ToImmutableArray(),
            Players!.Select(player => player.ToPlayer()).ToImmutableArray(),
            CurrentPlayerIndex,
            TurnNumber,
            Phase);
    }

    private sealed class CellDto
    {
        public int Id { get; set; }
        public GameColor Color { get; set; }
        public int? OwnerId { get; set; }

        public static CellDto FromCell(CellState cell) => new()
        {
            Id = cell.Id,
            Color = cell.Color,
            OwnerId = cell.OwnerId
        };

        public CellState ToCell() => new(Id, Color, OwnerId);
    }

    private sealed class PlayerDto
    {
        public int Id { get; set; }
        public PlayerKind Kind { get; set; }
        public int StartCellId { get; set; }
        public GameColor Color { get; set; }

        public static PlayerDto FromPlayer(PlayerState player) => new()
        {
            Id = player.Id,
            Kind = player.Kind,
            StartCellId = player.StartCellId,
            Color = player.Color
        };

        public PlayerState ToPlayer() => new(Id, Kind, StartCellId, Color);
    }
}
