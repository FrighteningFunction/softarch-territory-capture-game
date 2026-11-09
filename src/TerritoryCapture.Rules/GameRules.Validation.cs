
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

        // Player collection
        if (state.Players.IsDefault ||
            state.Players.Length < 2 ||
            state.Players.Length > 4)
        {
            AddProblem("Game must contain between 2 and 4 players.");
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

        for (var i = 0; i < state.Players.Length; i++)
        {
            var player = state.Players[i];

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
        }

        return Result();
    }
}
