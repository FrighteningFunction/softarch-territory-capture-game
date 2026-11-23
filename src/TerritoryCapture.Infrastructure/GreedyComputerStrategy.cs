using TerritoryCapture.Contracts;

namespace TerritoryCapture.Infrastructure;

/// <summary>Selects the greatest full capture using the shared rules engine.</summary>
public sealed class GreedyComputerStrategy : IComputerStrategy
{
    public GameColor ChooseColor(GameState state, IGameRules rules)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(rules);
        if (state.Phase != GamePhase.Running || state.Players.IsDefaultOrEmpty ||
            state.CurrentPlayerIndex < 0 || state.CurrentPlayerIndex >= state.Players.Length ||
            state.Players[state.CurrentPlayerIndex].Kind != PlayerKind.Computer)
        {
            throw new ArgumentException("A running computer turn is required.", nameof(state));
        }

        var colors = rules.LegalColors(state);
        if (colors.IsDefaultOrEmpty)
            throw new InvalidOperationException("A running game must have a legal color.");

        var playerId = state.Players[state.CurrentPlayerIndex].Id;
        GameColor? bestColor = null;
        var bestGain = -1;
        // Numeric IDs define the palette order, independently of dependency ordering.
        foreach (var color in colors.OrderBy(color => (int)color))
        {
            var result = rules.ApplyMove(state, playerId, color);
            if (result.Error != RuleError.None || result.State is null || result.CapturedCount < 0)
                throw new InvalidOperationException("Rules rejected a legal simulated move.");

            if (result.CapturedCount > bestGain)
            {
                bestColor = color;
                bestGain = result.CapturedCount;
            }
        }

        return bestColor!.Value;
    }
}
