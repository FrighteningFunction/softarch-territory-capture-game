using System.Collections.Immutable;
using TerritoryCapture.Contracts;

namespace TerritoryCapture.Rules;

public sealed class GameRules : IGameRules
{
    private const int BoardWidth = 12;
    private const int BoardHeight = 10;
    private static readonly GameColor[] Palette = Enum.GetValues<GameColor>();
    private static readonly (int Column, int Row)[] Starts =
        [(0, 0), (11, 9), (11, 0), (0, 9)];

    public StateResult Create(NewGameOptions options, int seed)
    {
        if (options is null
            || options.Players.IsDefault
            || options.Players.Length is < 2 or > 4
            || !options.Players.Contains(PlayerKind.Human)
            || options.Players.Any(kind => !Enum.IsDefined(kind)))
        {
            return new StateResult(null, RuleError.InvalidOptions);
        }

        var random = new Random(seed);
        var colors = Palette.ToArray();
        for (var index = colors.Length - 1; index > 0; index--)
        {
            var other = random.Next(index + 1);
            (colors[index], colors[other]) = (colors[other], colors[index]);
        }
        var cells = Enumerable.Range(0, BoardWidth * BoardHeight)
            .Select(id => new CellState(id, Palette[random.Next(Palette.Length)], null))
            .ToArray();
        var players = ImmutableArray.CreateBuilder<PlayerState>(options.Players.Length);

        for (var index = 0; index < options.Players.Length; index++)
        {
            var (column, row) = Starts[index];
            var startCellId = row * BoardWidth + column;
            var player = new PlayerState(index, options.Players[index], startCellId, colors[index]);
            players.Add(player);
            cells[startCellId] = cells[startCellId] with { Color = player.Color };
        }

        for (var index = 0; index < players.Count; index++)
        {
            CaptureInitialComponent(cells, players[index]);
        }

        var immutableCells = cells.ToImmutableArray();
        var neutralCount = immutableCells.Count(cell => cell.OwnerId is null);
        var state = new GameState(
            BoardWidth,
            BoardHeight,
            immutableCells,
            players.MoveToImmutable(),
            0,
            0,
            neutralCount == 0 ? GamePhase.Finished : GamePhase.Running);
        return new StateResult(state, RuleError.None);
    }

    public ImmutableArray<GameColor> LegalColors(GameState state)
    {
        if (state.Phase == GamePhase.Finished
            || state.CurrentPlayerIndex < 0
            || state.CurrentPlayerIndex >= state.Players.Length)
        {
            return [];
        }

        var usedColors = state.Players.Select(player => player.Color).ToHashSet();
        return Palette.Where(color => !usedColors.Contains(color)).ToImmutableArray();
    }

    public MoveResult ApplyMove(GameState state, int playerId, GameColor color)
    {
        var validation = Validate(state);
        if (!validation.IsValid)
        {
            return new MoveResult(null, 0, RuleError.InvalidState);
        }

        if (state.Phase == GamePhase.Finished)
        {
            return new MoveResult(null, 0, RuleError.FinishedGame);
        }

        var currentPlayer = state.Players[state.CurrentPlayerIndex];
        if (!state.Players.Any(player => player.Id == playerId))
        {
            return new MoveResult(null, 0, RuleError.UnknownPlayer);
        }

        if (currentPlayer.Id != playerId)
        {
            return new MoveResult(null, 0, RuleError.WrongTurn);
        }

        if (!Enum.IsDefined(color) || !LegalColors(state).Contains(color))
        {
            return new MoveResult(null, 0, RuleError.IllegalColor);
        }

        var cells = state.Cells.ToBuilder();
        for (var index = 0; index < cells.Count; index++)
        {
            if (cells[index].OwnerId == playerId)
            {
                cells[index] = cells[index] with { Color = color };
            }
        }

        var captured = new HashSet<int>();
        var queue = new Queue<int>();
        foreach (var cell in cells)
        {
            if (cell.OwnerId != playerId)
            {
                continue;
            }

            foreach (var neighborId in Neighbors(cell.Id, BoardWidth, BoardHeight))
            {
                var neighbor = cells[neighborId];
                if (neighbor.OwnerId is null && neighbor.Color == color && captured.Add(neighborId))
                {
                    queue.Enqueue(neighborId);
                }
            }
        }

        while (queue.TryDequeue(out var cellId))
        {
            foreach (var neighborId in Neighbors(cellId, BoardWidth, BoardHeight))
            {
                var neighbor = cells[neighborId];
                if (neighbor.OwnerId is null && neighbor.Color == color && captured.Add(neighborId))
                {
                    queue.Enqueue(neighborId);
                }
            }
        }

        foreach (var cellId in captured)
        {
            cells[cellId] = cells[cellId] with { OwnerId = playerId };
        }

        var nextTurn = state.TurnNumber + 1;
        var neutralCount = cells.Count(cell => cell.OwnerId is null);
        var finished = neutralCount == 0;
        var nextPlayerIndex = finished
            ? state.CurrentPlayerIndex
            : (state.CurrentPlayerIndex + 1) % state.Players.Length;
        var successor = state with
        {
            Cells = cells.ToImmutable(),
            Players = state.Players.SetItem(
                state.CurrentPlayerIndex, currentPlayer with { Color = color }),
            CurrentPlayerIndex = nextPlayerIndex,
            TurnNumber = nextTurn,
            Phase = finished ? GamePhase.Finished : GamePhase.Running
        };
        return new MoveResult(successor, captured.Count, RuleError.None);
    }

    public ValidationResult Validate(GameState state)
    {
        var problems = ImmutableArray.CreateBuilder<string>();
        if (state.Width != BoardWidth || state.Height != BoardHeight)
        {
            problems.Add("Board dimensions must be 12 by 10.");
        }
        if (state.Cells.IsDefault || state.Cells.Length != BoardWidth * BoardHeight)
        {
            problems.Add("The board must contain exactly 120 cells.");
        }
        if (state.Players.IsDefault || state.Players.Length is < 2 or > 4)
        {
            problems.Add("There must be between two and four players.");
        }
        if (state.TurnNumber < 0 || state.TurnNumber == long.MaxValue)
        {
            problems.Add("Turn number must be nonnegative and incrementable.");
        }
        if (state.Players.IsDefaultOrEmpty
            || state.CurrentPlayerIndex < 0
            || state.CurrentPlayerIndex >= state.Players.Length)
        {
            problems.Add("Current player index is out of range.");
        }
        if (!Enum.IsDefined(state.Phase))
        {
            problems.Add("Game phase is undefined.");
        }

        if (state.Players.IsDefaultOrEmpty)
        {
            return new ValidationResult(false, problems.ToImmutable());
        }

        if (!state.Players.Any(player => player.Kind == PlayerKind.Human))
        {
            problems.Add("At least one player must be human.");
        }

        var playersById = state.Players
            .GroupBy(player => player.Id)
            .ToDictionary(group => group.Key, group => group.First());
        var playerIds = playersById.Keys.ToHashSet();
        var playerColors = new HashSet<GameColor>();
        for (var index = 0; index < state.Players.Length; index++)
        {
            var player = state.Players[index];
            if (player.Id != index)
            {
                problems.Add("Player IDs must be unique and sequential.");
            }
            if (!Enum.IsDefined(player.Kind) || !Enum.IsDefined(player.Color))
            {
                problems.Add($"Player {index} has an undefined kind or color.");
            }
            if (!playerColors.Add(player.Color))
            {
                problems.Add("Player colors must be unique.");
            }
            if (index < Starts.Length)
            {
                var (column, row) = Starts[index];
                if (player.StartCellId != row * BoardWidth + column)
                {
                    problems.Add($"Player {index} has an invalid start position.");
                }
            }
        }

        if (state.Cells.IsDefault || state.Cells.Length != BoardWidth * BoardHeight)
        {
            return new ValidationResult(false, problems.ToImmutable());
        }

        for (var index = 0; index < state.Cells.Length; index++)
        {
            var cell = state.Cells[index];
            if (cell.Id != index)
            {
                problems.Add("Cell IDs must be sequential and ordered.");
            }
            if (!Enum.IsDefined(cell.Color))
            {
                problems.Add($"Cell {index} has an undefined color.");
            }
            if (cell.OwnerId is int ownerId)
            {
                if (!playerIds.Contains(ownerId))
                {
                    problems.Add($"Cell {index} refers to an unknown owner.");
                }
                else if (playersById[ownerId].Color != cell.Color)
                {
                    problems.Add($"Owned cell {index} differs from its owner's color.");
                }
            }
        }

        foreach (var player in state.Players)
        {
            if (player.StartCellId < 0 || player.StartCellId >= state.Cells.Length
                || state.Cells[player.StartCellId].OwnerId != player.Id)
            {
                problems.Add($"Player {player.Id} does not own its start cell.");
            }
            else if (!IsTerritoryConnected(state.Cells, player.Id, player.StartCellId))
            {
                problems.Add($"Player {player.Id}'s territory is disconnected.");
            }

            foreach (var cell in state.Cells.Where(cell => cell.OwnerId == player.Id))
            {
                if (Neighbors(cell.Id, BoardWidth, BoardHeight).Any(neighborId =>
                    state.Cells[neighborId].OwnerId is null
                    && state.Cells[neighborId].Color == player.Color))
                {
                    problems.Add($"Player {player.Id}'s territory is not capture-closed.");
                    break;
                }
            }
        }

        var neutralCount = state.Cells.Count(cell => cell.OwnerId is null);
        if (state.Phase == GamePhase.Finished && neutralCount != 0
            || state.Phase == GamePhase.Running && neutralCount == 0)
        {
            problems.Add("Game phase does not match the neutral cell count.");
        }

        return new ValidationResult(problems.Count == 0, problems.ToImmutable());
    }

    public GameSummary Summarize(GameState state)
    {
        var scores = state.Players
            .Select(player => new PlayerScore(player.Id,
                state.Cells.Count(cell => cell.OwnerId == player.Id)))
            .ToImmutableArray();
        var neutralCount = state.Cells.Count(cell => cell.OwnerId is null);
        var maximumScore = scores.IsEmpty ? 0 : scores.Max(score => score.Score);
        var winners = state.Phase == GamePhase.Finished
            ? scores.Where(score => score.Score == maximumScore)
                .Select(score => score.PlayerId).ToImmutableArray()
            : [];
        return new GameSummary(scores, neutralCount, winners);
    }

    internal static IEnumerable<int> Neighbors(int id, int width, int height)
    {
        var column = id % width;
        var row = id / width;
        (int Column, int Row)[] offsets = row % 2 == 0
            ? [(-1, 0), (1, 0), (-1, -1), (0, -1), (-1, 1), (0, 1)]
            : [(-1, 0), (1, 0), (0, -1), (1, -1), (0, 1), (1, 1)];

        foreach (var (columnOffset, rowOffset) in offsets)
        {
            var neighborColumn = column + columnOffset;
            var neighborRow = row + rowOffset;
            if (neighborColumn >= 0 && neighborColumn < width
                && neighborRow >= 0 && neighborRow < height)
            {
                yield return neighborRow * width + neighborColumn;
            }
        }
    }

    private static void CaptureInitialComponent(CellState[] cells, PlayerState player)
    {
        var queue = new Queue<int>();
        queue.Enqueue(player.StartCellId);
        cells[player.StartCellId] = cells[player.StartCellId] with { OwnerId = player.Id };
        while (queue.TryDequeue(out var cellId))
        {
            foreach (var neighborId in Neighbors(cellId, BoardWidth, BoardHeight))
            {
                if (cells[neighborId].OwnerId is null && cells[neighborId].Color == player.Color)
                {
                    cells[neighborId] = cells[neighborId] with { OwnerId = player.Id };
                    queue.Enqueue(neighborId);
                }
            }
        }
    }

    private static bool IsTerritoryConnected(
        ImmutableArray<CellState> cells, int playerId, int startCellId)
    {
        var visited = new HashSet<int> { startCellId };
        var queue = new Queue<int>();
        queue.Enqueue(startCellId);
        while (queue.TryDequeue(out var cellId))
        {
            foreach (var neighborId in Neighbors(cellId, BoardWidth, BoardHeight))
            {
                if (cells[neighborId].OwnerId == playerId && visited.Add(neighborId))
                {
                    queue.Enqueue(neighborId);
                }
            }
        }

        return visited.Count == cells.Count(cell => cell.OwnerId == playerId);
    }
}
