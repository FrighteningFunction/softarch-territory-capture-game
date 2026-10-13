using System.Collections.Immutable;
using TerritoryCapture.Contracts;

namespace TerritoryCapture.Web.Development;

// C's scripted preview: excluded from Release compilation, never real game rules.
public sealed class DemoController : IGameController
{
    public GameView View { get; private set; } = Setup();
    public event Action? Changed;
    public ValueTask<CommandResult> StartAsync(NewGameOptions options)
    {
        var computer = !options.Players.IsDefaultOrEmpty && options.Players[0] == PlayerKind.Computer;
        Show(CreateState(computer, false));
        return Success();
    }
    public ValueTask<CommandResult> ResumeAsync()
    {
        Show(CreateState(false, true));
        return Success();
    }
    public ValueTask<CommandResult> ReturnToSetupAsync()
    {
        View = Setup();
        Changed?.Invoke();
        return Success();
    }
    public ValueTask<CommandResult> ChooseColorAsync(ViewToken expected, GameColor color) =>
        ValueTask.FromResult(new CommandResult(CommandError.NotAllowed));
    public ValueTask<CommandResult> SaveAsync(ViewToken expected) =>
        ValueTask.FromResult(new CommandResult(CommandError.NotAllowed));

    private void Show(GameState state)
    {
        var finished = state.Phase == GamePhase.Finished;
        var computer = state.Players[0].Kind == PlayerKind.Computer;
        View = new(new(Guid.NewGuid(), 0), finished ? Screen.Result : Screen.Game,
            state, computer, !finished && !computer,
            finished || computer ? [] : [GameColor.Orange, GameColor.Yellow, GameColor.Green, GameColor.Purple],
            state.Players.Select(p => new PlayerView(p.Id, p.Kind, p.Color,
                state.Cells.Count(c => c.OwnerId == p.Id))).ToImmutableArray(),
            state.Cells.Count(c => c.OwnerId is null), finished ? [0, 1] : [], CommandError.None);
        Changed?.Invoke();
    }
    private static GameState CreateState(bool computer, bool finished) => new(12, 10,
        Enumerable.Range(0, 120).Select(id => finished
            ? new CellState(id, id % 12 < 6 ? GameColor.Red : GameColor.Blue, id % 12 < 6 ? 0 : 1)
            : id switch
            {
                0 => new CellState(id, GameColor.Red, 0),
                119 => new CellState(id, GameColor.Blue, 1),
                _ => new CellState(id, GameColor.Yellow, null)
            }).ToImmutableArray(),
        [new(0, computer ? PlayerKind.Computer : PlayerKind.Human, 0, GameColor.Red),
         new(1, PlayerKind.Human, 119, GameColor.Blue)],
        0, finished ? 20 : 0, finished ? GamePhase.Finished : GamePhase.Running);
    private static GameView Setup() => new(new(Guid.NewGuid(), 0), Screen.Setup,
        null, false, false, [], [], 0, [], CommandError.None);
    private static ValueTask<CommandResult> Success() => ValueTask.FromResult(new CommandResult(CommandError.None));
}
