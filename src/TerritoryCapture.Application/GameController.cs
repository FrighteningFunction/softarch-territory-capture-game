using System.Collections.Immutable;
using TerritoryCapture.Contracts;

namespace TerritoryCapture.Application;

public sealed class GameController : IGameController, IDisposable
{
    private readonly IGameRules _gameRules;
    private readonly IComputerStrategy _computerStrategy;
    private readonly IGameSaveStore _saveStore;
    
    private GameView _gameView;
    private CancellationTokenSource? _runnerCTS;
    
    public GameView View => _gameView;
    public event Action? Changed;

    public GameController(IGameRules gameRules, IComputerStrategy computerStrategy, IGameSaveStore saveStore)
    {
        _gameRules = gameRules;
        _computerStrategy = computerStrategy;
        _saveStore = saveStore;
        _gameView = CreateSetupView(Guid.NewGuid());
    }

    private GameView CreateSetupView(Guid sessionId)
    {
        return new GameView(
            Token: new ViewToken(sessionId, 0),
            Screen: Screen.Setup,
            State: null,
            IsBusy: false,
            CanSave: false,
            LegalColors: ImmutableArray<GameColor>.Empty,
            Players: ImmutableArray<PlayerView>.Empty,
            NeutralCount: 0,
            WinnerIds: ImmutableArray<int>.Empty,
            BackgroundError: CommandError.None
        );
    }

    private GameView BuildGameView(
        Guid sessionId,
        long revision,
        Screen screen,
        GameState state,
        bool isBusy,
        CommandError backgroundError)
    {
        var isHumanTurn = state.Phase == GamePhase.Running &&
            state.Players[state.CurrentPlayerIndex].Kind == PlayerKind.Human;
        var canSave = !isBusy && isHumanTurn && screen == Screen.Game;
        var legalColors = (isBusy && isHumanTurn && screen == Screen.Game) 
            ? _gameRules.LegalColors(state) :  ImmutableArray<GameColor>.Empty;
        var summary = _gameRules.Summarize(state);
        var players = state.Players.Select((p, i) =>
            new PlayerView(p.Id, p.Kind, p.Color, summary.Scores[i].Score)).ToImmutableArray();
        return new GameView(
            new ViewToken(sessionId, revision),
            screen,
            state,
            isBusy,
            canSave,
            legalColors,
            players,
            summary.NeutralCount,
            summary.WinnerIds,
            backgroundError
        );
    }

    public void PublishView(GameView newView)
    {
        _gameView = newView;
        Changed?.Invoke();
    }

    public void Dispose()
    {
        _runnerCTS?.Cancel();
        _runnerCTS?.Dispose();
    }

    public ValueTask<CommandResult> StartAsync(NewGameOptions options)
    {
        if (_gameView.Screen != Screen.Setup || _gameView.IsBusy)
            return ValueTask.FromResult(new CommandResult(CommandError.NotAllowed));
        var seed = Random.Shared.Next();
        var result = _gameRules.Create(options, seed);
        if (result.Error != RuleError.None)
            return ValueTask.FromResult(new CommandResult(CommandError.InvalidOptions));
        StartNewSession(result.State!);
        return ValueTask.FromResult(new CommandResult(CommandError.None));
    }

    public ValueTask<CommandResult> ReturnToSetupAsync()
    {
        _runnerCTS?.Cancel();
        PublishView(CreateSetupView(Guid.NewGuid()));
        return ValueTask.FromResult(new CommandResult(CommandError.None));
    }

    public ValueTask<CommandResult> ChooseColorAsync(ViewToken expected, GameColor color)
    {
        if (_gameView.Token != expected)
            return ValueTask.FromResult(new CommandResult(CommandError.StaleView));
        if (_gameView.Screen != Screen.Game || _gameView.IsBusy || _gameView.State?.Phase != GamePhase.Running)
            return ValueTask.FromResult(new CommandResult(CommandError.NotAllowed));
        var state = _gameView.State;
        var currentPlayer = state.Players[state.CurrentPlayerIndex];
        if (currentPlayer.Kind != PlayerKind.Human)
            return ValueTask.FromResult(new CommandResult(CommandError.NotAllowed));
        var moveResult = _gameRules.ApplyMove(state, currentPlayer.Id, color);
        if (moveResult.Error != RuleError.None)
            return ValueTask.FromResult(new CommandResult(CommandError.IllegalColor));
        var nextScreen = moveResult.State!.Phase == GamePhase.Finished ? Screen.Result : Screen.Game;
        PublishView(BuildGameView(expected.SessionId, expected.Revision + 1, nextScreen, moveResult.State, false, CommandError.None));
        return ValueTask.FromResult(new CommandResult(CommandError.None));
    }

    public async ValueTask<CommandResult> SaveAsync(ViewToken expected)
    {
        if (_gameView.Token != expected)
            return new CommandResult(CommandError.StaleView);
        if (!_gameView.CanSave)
            return new CommandResult(CommandError.NotAllowed);
        var stateToSave = _gameView.State!;
        var sessionId = expected.SessionId;
        var newRevision = expected.Revision + 1;
        PublishView(BuildGameView(sessionId, newRevision, _gameView.Screen, stateToSave, true, CommandError.None));
        StoreResult storeResult;
        try
        {
            storeResult = await _saveStore.SaveAsync(new GameSnapshot(1, stateToSave), _runnerCTS!.Token);
        }
        finally
        {
            if(_gameView.Token.SessionId == sessionId && _gameView.Token.Revision == newRevision)
                PublishView(BuildGameView(sessionId, newRevision + 1, _gameView.Screen, stateToSave, false, CommandError.None));
        }
        return storeResult.Error switch
        {
            StoreError.None => new CommandResult(CommandError.None),
            StoreError.Unavailable => new CommandResult(CommandError.StorageUnavailable),
            _ => new CommandResult(CommandError.UnexpectedFailure)
        };
    }

    public async ValueTask<CommandResult> ResumeAsync()
    {
        if (_gameView.Screen != Screen.Setup || _gameView.IsBusy)
            return new CommandResult(CommandError.NotAllowed);
        PublishView(_gameView with {IsBusy = true, Token = new ViewToken(_gameView.Token.SessionId, _gameView.Token.Revision + 1)});
        var loadResult = await _saveStore.LoadAsync(CancellationToken.None);
        if (loadResult.Error != StoreError.None || loadResult.Snapshot == null)
        {
            PublishView(CreateSetupView(Guid.NewGuid()));
            return new CommandResult(CommandError.MissingSave);
        }
        var state = loadResult.Snapshot.State;
        var validation = _gameRules.Validate(state);
        if (!validation.IsValid || state.Phase != GamePhase.Running ||
            state.Players[state.CurrentPlayerIndex].Kind != PlayerKind.Human)
        {
            PublishView(CreateSetupView(Guid.NewGuid()));
            return new CommandResult(CommandError.InvalidSave);
        }
        StartNewSession(state);
        return new CommandResult(CommandError.None);
    }

    private void StartNewSession(GameState state)
    {
        _runnerCTS?.Cancel();
        _runnerCTS = new CancellationTokenSource();
        var sessionId = Guid.NewGuid();
        PublishView(BuildGameView(sessionId, 1, state.Phase == GamePhase.Finished ? Screen.Result : Screen.Game, state, false, CommandError.None));
        CheckAndStartComputerRunner(sessionId);
    }

    private void CheckAndStartComputerRunner(Guid sessionId)
    {
        var state = _gameView.State;
        if (state == null || state.Phase != GamePhase.Running) return;
        if(state.Players[state.CurrentPlayerIndex].Kind != PlayerKind.Computer)
            _ = RunComputerLoopAsync(sessionId, _runnerCTS!.Token);
    }

    private async Task RunComputerLoopAsync(Guid sessionId, CancellationToken cancelToken)
    {
        try
        {
            while (!cancelToken.IsCancellationRequested)
            {
                var state = _gameView.State;
                if (state == null || state.Phase != GamePhase.Running || _gameView.Token.SessionId == sessionId) break;
                if (state.Players[state.CurrentPlayerIndex].Kind != PlayerKind.Computer) break;
                await Task.Delay(1, cancelToken);
                var color = _computerStrategy.ChooseColor(state, _gameRules);
                if (cancelToken.IsCancellationRequested || _gameView.Token.SessionId != sessionId) break;
                var result = _gameRules.ApplyMove(state, state.Players[state.CurrentPlayerIndex].Id, color);
                if (result.Error != RuleError.None) break;
                var nextScreen = result.State!.Phase == GamePhase.Finished ? Screen.Result : Screen.Game;
                PublishView(BuildGameView(sessionId, _gameView.Token.Revision + 1, nextScreen, result.State, false,
                    CommandError.None));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            if(_gameView.Token.SessionId == sessionId)
                PublishView(BuildGameView(sessionId, _gameView.Token.Revision + 1, _gameView.Screen, _gameView.State!, false, CommandError.UnexpectedFailure));
        }
    }
}