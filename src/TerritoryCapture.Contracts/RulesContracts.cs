using System.Collections.Immutable;

namespace TerritoryCapture.Contracts;

public enum GameColor { Red, Orange, Yellow, Green, Blue, Purple }
public enum PlayerKind { Human, Computer }
public enum GamePhase { Running, Finished }

public sealed record CellState(int Id, GameColor Color, int? OwnerId);
public sealed record PlayerState(
    int Id, PlayerKind Kind, int StartCellId, GameColor Color);
public sealed record NewGameOptions(ImmutableArray<PlayerKind> Players);

public sealed record GameState(
    int Width,
    int Height,
    ImmutableArray<CellState> Cells,
    ImmutableArray<PlayerState> Players,
    int CurrentPlayerIndex,
    long TurnNumber,
    GamePhase Phase);

public enum RuleError {
    None, InvalidOptions, InvalidState, FinishedGame,
    UnknownPlayer, WrongTurn, IllegalColor
}
public sealed record StateResult(GameState? State, RuleError Error);
public sealed record MoveResult(
    GameState? State, int CapturedCount, RuleError Error);
public sealed record ValidationResult(
    bool IsValid, ImmutableArray<string> Problems);
public sealed record PlayerScore(int PlayerId, int Score);
public sealed record GameSummary(
    ImmutableArray<PlayerScore> Scores,
    int NeutralCount,
    ImmutableArray<int> WinnerIds);

public interface IGameRules {
    StateResult Create(NewGameOptions options, int seed);
    ImmutableArray<GameColor> LegalColors(GameState state);
    MoveResult ApplyMove(GameState state, int playerId, GameColor color);
    ValidationResult Validate(GameState state);
    GameSummary Summarize(GameState state);
}
