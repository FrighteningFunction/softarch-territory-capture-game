
using System.Collections.Immutable;
using TerritoryCapture.Contracts;

namespace TerritoryCapture.Rules;

public sealed partial class GameRules
{
    public ValidationResult Validate(GameState state)
    {
        var problems = new List<string>();

        void AddProblem(string problem) => problems.Add(problem);

        ValidationResult Result() =>
            new(problems.Count == 0, problems.ToImmutableArray());

        if (state is null)
        {
            AddProblem("Game state cannot be null.");
            return Result();
        }

        // Board dimensions
        if (state.Width != BoardWidth || state.Height != BoardHeight)
        {
            AddProblem($"Board dimensions must be {BoardWidth}x{BoardHeight}.");
            return Result();
        }

        // Cell collection
        if (state.Cells.IsDefault)
        {
            AddProblem("Cell collection must not be default.");
            return Result();
        }

        if (state.Cells.Length != BoardWidth * BoardHeight)
        {
            AddProblem($"Board must contain exactly {BoardWidth * BoardHeight} cells.");
            return Result();
        }

        // Null cell entries
        if (state.Cells.Any(cell => cell is null))
        {
            AddProblem("Cell entry cannot be null.");
            return Result();
        }

        // Player collection
        if (state.Players.IsDefault ||
            state.Players.Length < 2 ||
            state.Players.Length > 4)
        {
            AddProblem("Game must contain between 2 and 4 players.");
            return Result();
        }

        if (state.Players.Any(player => player is null))
        {
            AddProblem("Player entry cannot be null.");
            return Result();
        }

        if (!state.Players.Any(player => player.Kind == PlayerKind.Human))
            AddProblem("At least one player must be human.");

        // Game metadata
        if (state.CurrentPlayerIndex < 0 ||
            state.CurrentPlayerIndex >= state.Players.Length)
        {
            AddProblem("Current player index is out of range.");
        }

        if (state.TurnNumber < 0)
            AddProblem("Turn number cannot be negative.");

        if (!Enum.IsDefined(state.Phase))
            AddProblem("Game phase is invalid.");

        // Player metadata
        var playerIds = new HashSet<int>();
        var playerById = new Dictionary<int, PlayerState>();

        for (var i = 0; i < state.Players.Length; i++)
        {
            var player = state.Players[i];
            playerById.TryAdd(player.Id, player);

            if (player.Id != i)
                AddProblem($"Player at index {i} must have ID {i}.");

            if (!playerIds.Add(player.Id))
                AddProblem($"Duplicate player ID: {player.Id}.");

            if (!Enum.IsDefined(player.Kind))
                AddProblem($"Player {player.Id} has an invalid player kind.");

            if (!Enum.IsDefined(player.Color) || !Palette.Contains(player.Color))
                AddProblem($"Player {player.Id} has an invalid color.");

            var expectedStartCell = StartingCells[state.Players.Length][i];

            if (player.StartCellId != expectedStartCell)
                AddProblem($"Player {player.Id} has an invalid starting cell.");

            if (player.StartCellId < 0 ||
                player.StartCellId >= state.Cells.Length)
            {
                AddProblem($"Player {player.Id} has an out-of-range starting cell.");
            }
        }

        if (state.Players
            .Select(player => player.Color)
            .Distinct()
            .Count() != state.Players.Length)
        {
            AddProblem("Player colors must be unique.");
        }

        // Cell metadata
        for (var i = 0; i < state.Cells.Length; i++)
        {
            var cell = state.Cells[i];

            if (cell.Id != i)
            {
                AddProblem($"Cell at index {i} must have ID {i}.");
                break;
            }

            if (!Enum.IsDefined(cell.Color) || !Palette.Contains(cell.Color))
                AddProblem($"Cell {cell.Id} has an invalid color.");

            if (cell.OwnerId is null)
                continue;

            if (!playerById.TryGetValue(cell.OwnerId.Value, out var owner))
            {
                AddProblem($"Cell {cell.Id} references unknown player {cell.OwnerId.Value}.");
                continue;
            }

            if (cell.Color != owner.Color)
            {
                AddProblem($"Cell {cell.Id} color does not match its owner's color.");
            }
        }

        // Starting cell ownership
        foreach (var player in state.Players)
        {
            if (player.StartCellId < 0 ||
                player.StartCellId >= state.Cells.Length)
            {
                continue;
            }

            if (state.Cells[player.StartCellId].OwnerId != player.Id)
            {
                AddProblem($"Player {player.Id} does not own their starting cell.");
            }
        }


        // Territory connectivity
        foreach (var player in state.Players)
        {
            if (player.StartCellId < 0 ||
                player.StartCellId >= state.Cells.Length ||
                state.Cells[player.StartCellId].OwnerId != player.Id)
            {
                continue;
            }

            var visited = new bool[state.Cells.Length];
            var queue = new Queue<int>();

            queue.Enqueue(player.StartCellId);
            visited[player.StartCellId] = true;

            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();

                foreach (var neighborId in GetNeighbors(
                    currentId,
                    state.Width,
                    state.Height))
                {
                    if (visited[neighborId])
                        continue;

                    visited[neighborId] = true;

                    if (state.Cells[neighborId].OwnerId != player.Id)
                        continue;

                    queue.Enqueue(neighborId);
                }
            }

            for (var i = 0; i < state.Cells.Length; i++)
            {
                if (state.Cells[i].OwnerId == player.Id && !visited[i])
                {
                    AddProblem(
                        $"Player {player.Id} has disconnected territory.");
                    break;
                }
            }
        }

        // Capture closure
        foreach (var player in state.Players)
        {
            var reportedNeutralCells = new HashSet<int>();

            for (var i = 0; i < state.Cells.Length; i++)
            {
                if (state.Cells[i].OwnerId != player.Id)
                    continue;

                foreach (var neighborId in GetNeighbors(
                    i,
                    state.Width,
                    state.Height))
                {
                    var neighbor = state.Cells[neighborId];

                    if (neighbor.OwnerId is null &&
                        neighbor.Color == player.Color &&
                        reportedNeutralCells.Add(neighborId))
                    {
                        AddProblem(
                            $"Neutral cell {neighborId} should already be captured by player {player.Id}.");
                    }
                }
            }
        }

        // Phase consistency
        var neutralCount = state.Cells.Count(
            cell => cell.OwnerId is null);

        if (state.Phase == GamePhase.Running && neutralCount == 0)
        {
            AddProblem(
                "Running game cannot have zero neutral cells.");
        }

        if (state.Phase == GamePhase.Finished && neutralCount > 0)
        {
            AddProblem(
                "Finished game cannot have neutral cells.");
        }

        return Result();
    }
}
