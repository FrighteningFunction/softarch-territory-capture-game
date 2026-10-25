using TerritoryCapture.Contracts;

namespace TerritoryCapture.Infrastructure;

public sealed class GreedyComputerStrategy : IComputerStrategy
{
    public GameColor ChooseColor(GameState state, IGameRules rules)
    {
        var legalColors = rules.LegalColors(state);
        if (state.Phase != GamePhase.Running
            || state.CurrentPlayerIndex < 0
            || state.CurrentPlayerIndex >= state.Players.Length
            || state.Players[state.CurrentPlayerIndex].Kind != PlayerKind.Computer
            || legalColors.IsDefaultOrEmpty)
        {
            throw new InvalidOperationException(
                "Greedy strategy requires a running computer turn with legal colors.");
        }

        var bestColor = legalColors[0];
        var bestGain = -1;
        foreach (var color in legalColors)
        {
            var result = rules.ApplyMove(
                state, state.Players[state.CurrentPlayerIndex].Id, color);
            if (result.Error != RuleError.None || result.State is null)
            {
                throw new InvalidOperationException(
                    $"Rules rejected legal color {color}: {result.Error}.");
            }
            if (result.CapturedCount > bestGain)
            {
                bestColor = color;
                bestGain = result.CapturedCount;
            }
        }

        return bestColor;
    }
}
