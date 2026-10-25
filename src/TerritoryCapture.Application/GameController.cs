using System.Collections.Immutable;
using TerritoryCapture.Contracts;

namespace TerritoryCapture.Application;

public sealed class GameController : IGameController, IDisposable
{
    private const int SnapshotVersion = 1;
    private readonly object _sync = new();
    private readonly IGameRules _rules;
    private readonly IComputerStrategy _strategy;
    private readonly IGameSaveStore? _saveStore;
    private readonly Func<CancellationToken, ValueTask> _yieldComputerTurn;
    private GameState? _state;
    private Screen _screen = Screen.Setup;
    private Guid _sessionId = Guid.NewGuid();
    private long _revision;
    private bool _isBusy;
    private CommandError _backgroundError;
    private CancellationTokenSource? _computerCancellation;
    private GameView _view;
    private bool _disposed;

    public GameController(
        IGameRules rules,
        IComputerStrategy strategy,
        IGameSaveStore? saveStore = null,
        Func<CancellationToken, ValueTask>? yieldComputerTurn = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(strategy);
        _rules = rules;
        _strategy = strategy;
        _saveStore = saveStore;
        _yieldComputerTurn = yieldComputerTurn ?? YieldToBrowserAsync;
        _view = ProjectView();
    }

    public GameView View
    {
        get
        {
            lock (_sync)
            {
                return _view;
            }
        }
    }

    public event Action? Changed;

    public ValueTask<CommandResult> StartAsync(NewGameOptions options)
    {
        CancellationTokenSource? runnerCancellation = null;
        GameState? stateToRun = null;
        Guid sessionId;
        long revision;
        lock (_sync)
        {
            if (_disposed || _screen != Screen.Setup || _isBusy)
            {
                return Result(CommandError.NotAllowed);
            }

            var creation = _rules.Create(options, Random.Shared.Next());
            if (creation.Error != RuleError.None || creation.State is null)
            {
                return Result(CommandError.InvalidOptions);
            }

            var validation = _rules.Validate(creation.State);
            if (!validation.IsValid)
            {
                return Result(CommandError.UnexpectedFailure);
            }

            _computerCancellation?.Dispose();
            _computerCancellation = null;
            _state = creation.State;
            _screen = _state.Phase == GamePhase.Finished ? Screen.Result : Screen.Game;
            _backgroundError = CommandError.None;
            _sessionId = Guid.NewGuid();
            _revision++;
            if (ShouldRunComputer(_state))
            {
                runnerCancellation = new CancellationTokenSource();
                _computerCancellation = runnerCancellation;
                _isBusy = true;
                _revision++;
                stateToRun = _state;
            }
            _view = ProjectView();
            sessionId = _sessionId;
            revision = _revision;
        }

        NotifyChanged();
        if (runnerCancellation is not null && stateToRun is not null)
        {
            _ = RunComputerLoopAsync(sessionId, stateToRun, revision, runnerCancellation);
        }

        return Result(CommandError.None);
    }

    public ValueTask<CommandResult> ChooseColorAsync(ViewToken expected, GameColor color)
    {
        CancellationTokenSource? runnerCancellation = null;
        GameState? stateToRun = null;
        Guid sessionId;
        long revision;
        lock (_sync)
        {
            var admissionError = CheckHumanCommand(expected);
            if (admissionError != CommandError.None)
            {
                return Result(admissionError);
            }

            var currentPlayer = _state!.Players[_state.CurrentPlayerIndex];
            var move = _rules.ApplyMove(_state, currentPlayer.Id, color);
            if (move.Error != RuleError.None || move.State is null)
            {
                return Result(move.Error == RuleError.IllegalColor
                    ? CommandError.IllegalColor
                    : CommandError.UnexpectedFailure);
            }

            _state = move.State;
            _screen = _state.Phase == GamePhase.Finished ? Screen.Result : Screen.Game;
            _revision++;
            if (ShouldRunComputer(_state))
            {
                runnerCancellation = new CancellationTokenSource();
                _computerCancellation = runnerCancellation;
                _isBusy = true;
                _revision++;
                stateToRun = _state;
            }
            _view = ProjectView();
            sessionId = _sessionId;
            revision = _revision;
        }

        NotifyChanged();
        if (runnerCancellation is not null && stateToRun is not null)
        {
            _ = RunComputerLoopAsync(sessionId, stateToRun, revision, runnerCancellation);
        }

        return Result(CommandError.None);
    }

    public async ValueTask<CommandResult> SaveAsync(ViewToken expected)
    {
        GameSnapshot snapshot;
        Guid sessionId;
        CancellationTokenSource operationCancellation;
        lock (_sync)
        {
            var admissionError = CheckHumanCommand(expected);
            if (admissionError != CommandError.None)
            {
                return new CommandResult(admissionError);
            }
            if (_saveStore is null)
            {
                return new CommandResult(CommandError.StorageUnavailable);
            }
            if (!_rules.Validate(_state!).IsValid)
            {
                System.Diagnostics.Trace.TraceError("Refusing to save an invalid active game state.");
                return new CommandResult(CommandError.UnexpectedFailure);
            }

            snapshot = new GameSnapshot(SnapshotVersion, _state!);
            sessionId = _sessionId;
            _isBusy = true;
            _revision++;
            operationCancellation = new CancellationTokenSource();
            _view = ProjectView();
        }

        NotifyChanged();
        CommandError error;
        try
        {
            var storeResult = await _saveStore.SaveAsync(snapshot, operationCancellation.Token);
            error = storeResult.Error switch
            {
                StoreError.None => CommandError.None,
                StoreError.Unavailable => CommandError.StorageUnavailable,
                StoreError.UnsupportedVersion => CommandError.UnsupportedSaveVersion,
                StoreError.Missing => CommandError.MissingSave,
                _ => CommandError.InvalidSave
            };
        }
        catch (OperationCanceledException) when (operationCancellation.IsCancellationRequested)
        {
            error = CommandError.StorageUnavailable;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError("Saving the game failed: {0}", exception);
            error = CommandError.UnexpectedFailure;
        }
        finally
        {
            operationCancellation.Dispose();
        }

        lock (_sync)
        {
            if (_disposed || _sessionId != sessionId)
            {
                return new CommandResult(CommandError.StaleView);
            }

            _isBusy = false;
            _revision++;
            _view = ProjectView();
        }

        NotifyChanged();
        return new CommandResult(error);
    }

    public async ValueTask<CommandResult> ResumeAsync()
    {
        Guid sessionId;
        CancellationTokenSource operationCancellation;
        lock (_sync)
        {
            if (_disposed || _screen != Screen.Setup || _isBusy)
            {
                return new CommandResult(CommandError.NotAllowed);
            }
            if (_saveStore is null)
            {
                return new CommandResult(CommandError.StorageUnavailable);
            }

            _isBusy = true;
            _revision++;
            sessionId = _sessionId;
            operationCancellation = new CancellationTokenSource();
            _view = ProjectView();
        }

        NotifyChanged();
        CommandError error;
        GameState? resumedState = null;
        try
        {
            var loadResult = await _saveStore.LoadAsync(operationCancellation.Token);
            error = loadResult.Error switch
            {
                StoreError.Missing => CommandError.MissingSave,
                StoreError.UnsupportedVersion => CommandError.UnsupportedSaveVersion,
                StoreError.Unavailable => CommandError.StorageUnavailable,
                StoreError.InvalidData => CommandError.InvalidSave,
                StoreError.None when loadResult.Snapshot is not null
                    => ValidateResumedState(loadResult.Snapshot, out resumedState),
                _ => CommandError.InvalidSave
            };
        }
        catch (OperationCanceledException) when (operationCancellation.IsCancellationRequested)
        {
            error = CommandError.StorageUnavailable;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError("Loading the game failed: {0}", exception);
            error = CommandError.UnexpectedFailure;
        }
        finally
        {
            operationCancellation.Dispose();
        }

        lock (_sync)
        {
            if (_disposed || _sessionId != sessionId)
            {
                return new CommandResult(CommandError.StaleView);
            }

            _isBusy = false;
            if (error == CommandError.None && resumedState is not null)
            {
                _state = resumedState;
                _screen = _state.Phase == GamePhase.Finished ? Screen.Result : Screen.Game;
                _sessionId = Guid.NewGuid();
                _backgroundError = CommandError.None;
            }
            _revision++;
            _view = ProjectView();
        }

        NotifyChanged();
        return new CommandResult(error);
    }

    public ValueTask<CommandResult> ReturnToSetupAsync()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return Result(CommandError.NotAllowed);
            }

            _computerCancellation?.Cancel();
            _computerCancellation?.Dispose();
            _computerCancellation = null;
            _state = null;
            _screen = Screen.Setup;
            _isBusy = false;
            _backgroundError = CommandError.None;
            _sessionId = Guid.NewGuid();
            _revision++;
            _view = ProjectView();
        }

        NotifyChanged();
        return Result(CommandError.None);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _computerCancellation?.Cancel();
            _computerCancellation?.Dispose();
            _computerCancellation = null;
            Changed = null;
        }
    }

    private async Task RunComputerLoopAsync(
        Guid sessionId,
        GameState initialState,
        long initialRevision,
        CancellationTokenSource cancellation)
    {
        Exception? failure = null;
        var state = initialState;
        var revision = initialRevision;
        try
        {
            while (ShouldRunComputer(state))
            {
                await _yieldComputerTurn(cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();

                var color = _strategy.ChooseColor(state, _rules);
                cancellation.Token.ThrowIfCancellationRequested();
                lock (_sync)
                {
                    if (_disposed || cancellation.IsCancellationRequested
                        || _sessionId != sessionId || _revision != revision
                        || !ReferenceEquals(_state, state))
                    {
                        return;
                    }

                    var player = state.Players[state.CurrentPlayerIndex];
                    var move = _rules.ApplyMove(state, player.Id, color);
                    if (move.Error != RuleError.None || move.State is null)
                    {
                        throw new InvalidOperationException("Computer strategy returned an illegal move.");
                    }

                    state = move.State;
                    _state = state;
                    _screen = state.Phase == GamePhase.Finished ? Screen.Result : Screen.Game;
                    _revision++;
                    revision = _revision;
                    _view = ProjectView();
                }

                NotifyChanged();
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            lock (_sync)
            {
                if (_sessionId == sessionId && ReferenceEquals(_computerCancellation, cancellation))
                {
                    _isBusy = false;
                    if (failure is not null)
                    {
                        _backgroundError = CommandError.UnexpectedFailure;
                    }
                    _computerCancellation = null;
                    _revision++;
                    _view = ProjectView();
                }
                cancellation.Dispose();
            }

            if (failure is not null)
            {
                System.Diagnostics.Trace.TraceError(
                    "Computer turn failed: {0}", failure);
            }
            NotifyChanged();
        }
    }

    private CommandError CheckHumanCommand(ViewToken expected)
    {
        if (_disposed || _screen != Screen.Game || _state is null
            || _state.Phase != GamePhase.Running)
        {
            return CommandError.NotAllowed;
        }
        if (expected.SessionId != _sessionId || expected.Revision != _revision)
        {
            return CommandError.StaleView;
        }
        if (_isBusy || _backgroundError != CommandError.None)
        {
            return CommandError.Busy;
        }
        if (_state.Players[_state.CurrentPlayerIndex].Kind != PlayerKind.Human)
        {
            return CommandError.NotAllowed;
        }

        return CommandError.None;
    }

    private CommandError ValidateResumedState(GameSnapshot snapshot, out GameState? state)
    {
        state = null;
        if (snapshot.Version != SnapshotVersion)
        {
            return CommandError.UnsupportedSaveVersion;
        }

        state = snapshot.State;
        if (state is null || !_rules.Validate(state).IsValid
            || state.Phase != GamePhase.Running
            || state.Players[state.CurrentPlayerIndex].Kind != PlayerKind.Human)
        {
            return CommandError.InvalidSave;
        }

        return CommandError.None;
    }

    private bool ShouldRunComputer(GameState state) =>
        state.Phase == GamePhase.Running
        && state.Players[state.CurrentPlayerIndex].Kind == PlayerKind.Computer;

    private GameView ProjectView()
    {
        var token = new ViewToken(_sessionId, _revision);
        if (_state is null)
        {
            return new GameView(token, Screen.Setup, null, _isBusy, false, [],
                [], 0, [], _backgroundError);
        }

        var summary = _rules.Summarize(_state);
        var players = _state.Players.Select(player => new PlayerView(
            player.Id,
            player.Kind,
            player.Color,
            summary.Scores.First(score => score.PlayerId == player.Id).Score))
            .ToImmutableArray();
        var isHumanTurn = _state.Phase == GamePhase.Running
            && _state.Players[_state.CurrentPlayerIndex].Kind == PlayerKind.Human;
        var legalColors = !_isBusy && _backgroundError == CommandError.None && isHumanTurn
            ? _rules.LegalColors(_state)
            : [];
        return new GameView(
            token,
            _screen,
            _state,
            _isBusy,
            !_isBusy && _backgroundError == CommandError.None
                && _saveStore is not null && isHumanTurn,
            legalColors,
            players,
            summary.NeutralCount,
            summary.WinnerIds,
            _backgroundError);
    }

    private void NotifyChanged() => Changed?.Invoke();

    private static ValueTask YieldToBrowserAsync(CancellationToken cancellationToken) =>
        YieldCoreAsync(cancellationToken);

    private static async ValueTask YieldCoreAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static ValueTask<CommandResult> Result(CommandError error) =>
        ValueTask.FromResult(new CommandResult(error));
}
