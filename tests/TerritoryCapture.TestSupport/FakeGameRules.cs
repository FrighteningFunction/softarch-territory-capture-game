using System.Collections.Immutable;
using TerritoryCapture.Contracts;

namespace TerritoryCapture.TestSupport;

public class FakeGameRules : IGameRules
{
    public GameState? NextState { get; set; }
    public RuleError NextError { get; set; } = RuleError.None;
    public bool IsValidResult { get; set; } = true;
    public ImmutableArray<GameColor> NextLegalColors { get; set; } = ImmutableArray<GameColor>.Empty;
    public GameSummary NextSummary { get; set; } = new(ImmutableArray<PlayerScore>.Empty, 0, ImmutableArray<int>.Empty);

    public StateResult Create(NewGameOptions options, int seed) => new(NextState, NextError);

    public ImmutableArray<GameColor> LegalColors(GameState state) => NextLegalColors;

    public MoveResult ApplyMove(GameState state, int playerId, GameColor color) => new(NextState, 1, NextError);

    public ValidationResult Validate(GameState state) => new(IsValidResult, ImmutableArray<string>.Empty);

    public GameSummary Summarize(GameState state) => NextSummary;
}