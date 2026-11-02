
using System.Collections.Immutable;
using TerritoryCapture.Contracts;

namespace TerritoryCapture.Rules;

public sealed partial class GameRules
{
    private const int BoardWidth = 12;
    private const int BoardHeight = 10;

    private static readonly ImmutableArray<GameColor> Palette =
    [
        GameColor.Red,
        GameColor.Orange,
        GameColor.Yellow,
        GameColor.Green,
        GameColor.Blue,
        GameColor.Purple
    ];

    private static readonly int[][] StartingCells =
    [
        [],
        [],
        [0, 119],
        [0, 11, 119],
        [0, 11, 108, 119]
    ];

    public StateResult Create(NewGameOptions options, int seed)
    {
        if (options is null ||
            options.Players.IsDefault ||
            options.Players.Length < 2 ||
            options.Players.Length > 4 ||
            options.Players.Any(kind => !Enum.IsDefined(kind)) ||
            !options.Players.Contains(PlayerKind.Human))
        {
            return new StateResult(null, RuleError.InvalidOptions);
        }

        var random = new Random(seed);

        // Assign a unique starting color to each player.
        var playerColors = Palette.ToArray();

        for (var i = playerColors.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (playerColors[i], playerColors[j]) =
                (playerColors[j], playerColors[i]);
        }

        var players = options.Players
            .Select((kind, index) => new PlayerState(
                index,
                kind,
                StartingCells[options.Players.Length][index],
                playerColors[index]))
            .ToImmutableArray();

        // Generate all cell colors from the complete palette.
        var cells = new CellState[BoardWidth * BoardHeight];

        for (var id = 0; id < cells.Length; id++)
        {
            cells[id] = new CellState(
                id,
                Palette[random.Next(Palette.Length)],
                null);
        }

        // Ensure that each starting cell has its owner's color.
        foreach (var player in players)
        {
            cells[player.StartCellId] = cells[player.StartCellId] with
            {
                Color = player.Color
            };
        }

        // Capture the initial connected components.
        foreach (var player in players)
        {
            CaptureInitialTerritory(cells, player);
        }

        var neutralCount = cells.Count(cell => cell.OwnerId is null);

        var state = new GameState(
            BoardWidth,
            BoardHeight,
            cells.ToImmutableArray(),
            players,
            CurrentPlayerIndex: 0,
            TurnNumber: 0,
            Phase: neutralCount == 0
                ? GamePhase.Finished
                : GamePhase.Running);

        return new StateResult(state, RuleError.None);
    }

    private static void CaptureInitialTerritory(
        CellState[] cells,
        PlayerState player)
    {
        var visited = new bool[cells.Length];
        var queue = new Queue<int>();

        queue.Enqueue(player.StartCellId);
        visited[player.StartCellId] = true;

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();

            cells[currentId] = cells[currentId] with
            {
                OwnerId = player.Id
            };

            foreach (var neighborId in GetNeighbors(
                currentId, BoardWidth, BoardHeight))
            {
                if (visited[neighborId])
                    continue;

                var neighbor = cells[neighborId];

                if (neighbor.OwnerId is not null ||
                    neighbor.Color != player.Color)
                {
                    continue;
                }

                visited[neighborId] = true;
                queue.Enqueue(neighborId);
            }
        }
    }

    internal static ImmutableArray<int> GetNeighbors(
        int cellId,
        int width,
        int height)
    {
        if (width <= 0 || height <= 0 ||
            cellId < 0 || cellId >= width * height)
        {
            return [];
        }

        var column = cellId % width;
        var row = cellId / width;

        var offsets = row % 2 == 0
            ? new (int Dc, int Dr)[]
            {
                (-1, 0), (1, 0),
                (-1, -1), (0, -1),
                (-1, 1), (0, 1)
            }
            : new (int Dc, int Dr)[]
            {
                (-1, 0), (1, 0),
                (0, -1), (1, -1),
                (0, 1), (1, 1)
            };

        var neighbors = ImmutableArray.CreateBuilder<int>();

        foreach (var (dc, dr) in offsets)
        {
            var neighborColumn = column + dc;
            var neighborRow = row + dr;

            if (neighborColumn < 0 || neighborColumn >= width ||
                neighborRow < 0 || neighborRow >= height)
            {
                continue;
            }

            neighbors.Add(neighborRow * width + neighborColumn);
        }

        return neighbors.ToImmutable();
    }
}
