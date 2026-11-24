
using System.Collections.Immutable;
using TerritoryCapture.Contracts;

namespace TerritoryCapture.Rules;

public sealed partial class GameRules
{
    public GameSummary Summarize(GameState state)
    {
        var scores = state.Players
            .Select(player => new PlayerScore(
                player.Id,
                state.Cells.Count(cell => cell.OwnerId == player.Id)))
            .ToImmutableArray();

        var neutralCount = state.Cells.Count(
            cell => cell.OwnerId is null);

        // An unfinished game has no winners yet.
        if (state.Phase != GamePhase.Finished || scores.IsEmpty)
        {
            return new GameSummary(
                scores,
                neutralCount,
                ImmutableArray<int>.Empty);
        }

        var highestScore = scores.Max(score => score.Score);

        var winnerIds = scores
            .Where(score => score.Score == highestScore)
            .Select(score => score.PlayerId)
            .ToImmutableArray();

        return new GameSummary(
            scores,
            neutralCount,
            winnerIds);
    }
}
