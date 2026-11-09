
using System.Collections.Immutable;
using TerritoryCapture.Contracts;

namespace TerritoryCapture.Rules;

public sealed partial class GameRules
{
    public ImmutableArray<GameColor> LegalColors(GameState state)
    {
        if (state is null ||
            state.Phase != GamePhase.Running ||
            state.Players.IsDefaultOrEmpty ||
            state.CurrentPlayerIndex < 0 ||
            state.CurrentPlayerIndex >= state.Players.Length)
        {
            return [];
        }

        return Palette
            .Where(color => state.Players.All(player => player.Color != color))
            .ToImmutableArray();
    }

    public MoveResult ApplyMove(
        GameState state,
        int playerId,
        GameColor color)
    {
        if (state is null ||
            state.Cells.IsDefault ||
            state.Players.IsDefaultOrEmpty ||
            state.Width <= 0 ||
            state.Height <= 0 ||
            (long)state.Width * state.Height != state.Cells.Length ||
            state.CurrentPlayerIndex < 0 ||
            state.CurrentPlayerIndex >= state.Players.Length ||
            state.TurnNumber < 0 ||
            state.TurnNumber == long.MaxValue)
        {
            return new MoveResult(null, 0, RuleError.InvalidState);
        }

        if (state.Phase == GamePhase.Finished)
        {
            return new MoveResult(null, 0, RuleError.FinishedGame);
        }

        var playerIndex = -1;

        for (var i = 0; i < state.Players.Length; i++)
        {
            if (state.Players[i].Id == playerId)
            {
                playerIndex = i;
                break;
            }
        }

        if (playerIndex == -1)
        {
            return new MoveResult(null, 0, RuleError.UnknownPlayer);
        }

        if (playerIndex != state.CurrentPlayerIndex)
        {
            return new MoveResult(null, 0, RuleError.WrongTurn);
        }

        if (!Enum.IsDefined(color) || !LegalColors(state).Contains(color))
        {
            return new MoveResult(null, 0, RuleError.IllegalColor);
        }

        var cells = state.Cells.ToBuilder();
        var players = state.Players.ToBuilder();

        var currentPlayer = players[playerIndex];

        // Change the color of every cell already owned by the player.
        for (var i = 0; i < cells.Count; i++)
        {
            if (cells[i].OwnerId == playerId)
            {
                cells[i] = cells[i] with { Color = color };
            }
        }

        // Start the capture search from all cells owned by this player.
        var visited = new bool[cells.Count];
        var queue = new Queue<int>();

        for (var i = 0; i < cells.Count; i++)
        {
            if (cells[i].OwnerId == playerId)
            {
                visited[i] = true;
                queue.Enqueue(i);
            }
        }

        var capturedCount = 0;

        // Capture connected neutral cells with the selected color.
        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();

            foreach (var neighborId in GetNeighbors(
                currentId,
                state.Width,
                state.Height))
            {
                if (visited[neighborId])
                {
                    continue;
                }

                visited[neighborId] = true;

                var neighbor = cells[neighborId];

                // Neutral cells of other colors and opponent cells
                // cannot be captured or traversed.
                if (neighbor.OwnerId is not null ||
                    neighbor.Color != color)
                {
                    continue;
                }

                cells[neighborId] = neighbor with
                {
                    OwnerId = playerId
                };

                capturedCount++;
                queue.Enqueue(neighborId);
            }
        }

        // Update the player's current color.
        players[playerIndex] = currentPlayer with
        {
            Color = color
        };

        var neutralCount = cells.Count(cell => cell.OwnerId is null);
        var isFinished = neutralCount == 0;

        var updatedState = state with
        {
            Cells = cells.ToImmutable(),
            Players = players.ToImmutable(),
            TurnNumber = state.TurnNumber + 1,
            CurrentPlayerIndex = isFinished
                ? state.CurrentPlayerIndex
                : (state.CurrentPlayerIndex + 1) % state.Players.Length,
            Phase = isFinished
                ? GamePhase.Finished
                : GamePhase.Running
        };

        return new MoveResult(
            updatedState,
            capturedCount,
            RuleError.None);
    }
}
