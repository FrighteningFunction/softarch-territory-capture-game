
using TerritoryCapture.Contracts;
using System.Collections.Immutable;

namespace TerritoryCapture.Rules;

public sealed partial class GameRules : IGameRules
{
     public ImmutableArray<GameColor> LegalColors(GameState state)
    {
        throw new NotImplementedException();
    }

    public MoveResult ApplyMove(GameState state, int playerId, GameColor color)
    {
        throw new NotImplementedException();
    }

    public ValidationResult Validate(GameState state)
    {
        throw new NotImplementedException();
    }

    public GameSummary Summarize(GameState state)
    {
        throw new NotImplementedException();
    }
}
