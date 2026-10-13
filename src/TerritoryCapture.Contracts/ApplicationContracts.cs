using System.Collections.Immutable;

namespace TerritoryCapture.Contracts;

public readonly record struct ViewToken(Guid SessionId, long Revision);
public enum Screen { Setup, Game, Result }
public sealed record PlayerView(
    int Id, PlayerKind Kind, GameColor Color, int Score);
public sealed record GameView(
    ViewToken Token,
    Screen Screen,
    GameState? State,
    bool IsBusy,
    bool CanSave,
    ImmutableArray<GameColor> LegalColors,
    ImmutableArray<PlayerView> Players,
    int NeutralCount,
    ImmutableArray<int> WinnerIds,
    CommandError BackgroundError);

public enum CommandError {
    None, InvalidOptions, NotAllowed, Busy, StaleView, IllegalColor,
    MissingSave, InvalidSave, UnsupportedSaveVersion, StorageUnavailable,
    UnexpectedFailure
}
public sealed record CommandResult(CommandError Error);

public interface IGameController {
    GameView View { get; }
    event Action? Changed;
    ValueTask<CommandResult> StartAsync(NewGameOptions options);
    ValueTask<CommandResult> ChooseColorAsync(
        ViewToken expected, GameColor color);
    ValueTask<CommandResult> SaveAsync(ViewToken expected);
    ValueTask<CommandResult> ResumeAsync();
    ValueTask<CommandResult> ReturnToSetupAsync();
}
