using System.Collections.Immutable;
using TerritoryCapture.Contracts;
using TerritoryCapture.TestSupport;

namespace TerritoryCapture.Application.Tests;

public class GameControllerTests
{
    private readonly FakeGameRules _gameRules;
    private readonly ControllableComputerStrategy _computerStrategy;
    private readonly InMemorySaveStore _saveStore;
    private readonly GameController _gameController;

    public GameControllerTests()
    {
        _gameRules = new FakeGameRules();
        _computerStrategy = new ControllableComputerStrategy();
        _saveStore = new InMemorySaveStore();
        _gameController = new GameController(_gameRules, _computerStrategy, _saveStore);
    }

    private GameState CreateDummyGameState(GamePhase phase, params PlayerKind[] playerKinds)
    {
        var players = new List<PlayerState>();
        var scores = new List<PlayerScore>();

        for (int i = 0; i < playerKinds.Length; i++)
        {
            players.Add(new PlayerState(i, playerKinds[i], i, (GameColor)i));
            scores.Add(new PlayerScore(i, 1));
        }
        
        _gameRules.NextSummary = new GameSummary(scores.ToImmutableArray(), 0, ImmutableArray<int>.Empty);
        return new GameState(12, 10, ImmutableArray<CellState>.Empty, players.ToImmutableArray(), 0, 1, phase);
    }
    
    //StartAsync tests
    [Fact]
    public async Task StartAsync_FromSetup_CreatesNewSession()
    {
        _gameRules.NextState = CreateDummyGameState(GamePhase.Running, PlayerKind.Human, PlayerKind.Computer);
        var result = await _gameController.StartAsync(new NewGameOptions(ImmutableArray.Create(PlayerKind.Human, PlayerKind.Computer)));

        Assert.Equal(CommandError.None, result.Error);
        Assert.Equal(Screen.Game, _gameController.View.Screen);
        Assert.Equal(1, _gameController.View.Token.Revision);
    }
    
    [Fact]
    public async Task StartAsync_WhenNotInSetup_ReturnsNotAllowed()
    {
        _gameRules.NextState = CreateDummyGameState(GamePhase.Running, PlayerKind.Human);
        await _gameController.StartAsync(new NewGameOptions(ImmutableArray.Create(PlayerKind.Human)));
        
        var result = await _gameController.StartAsync(new NewGameOptions(ImmutableArray.Create(PlayerKind.Human)));

        Assert.Equal(CommandError.NotAllowed, result.Error);
    }
    
    [Fact]
    public async Task StartAsync_WhenRulesCreateFails_ReturnsInvalidOptions()
    {
        _gameRules.NextError = RuleError.InvalidOptions;
        var result = await _gameController.StartAsync(new NewGameOptions(ImmutableArray<PlayerKind>.Empty));

        Assert.Equal(CommandError.InvalidOptions, result.Error);
        Assert.Equal(Screen.Setup, _gameController.View.Screen);
    }
    
    [Fact]
    public async Task StartAsync_InitiallyFinishedGame_GoesToResultScreen()
    {
        _gameRules.NextState = CreateDummyGameState(GamePhase.Finished, PlayerKind.Human);
        var result = await _gameController.StartAsync(new NewGameOptions(ImmutableArray.Create(PlayerKind.Human)));

        Assert.Equal(CommandError.None, result.Error);
        Assert.Equal(Screen.Result, _gameController.View.Screen);
    }

    //ChooseColorAsync tests
    [Fact]
    public async Task ChooseColorAsync_WithStaleToken_ReturnsStaleView()
    {
        _gameRules.NextState = CreateDummyGameState(GamePhase.Running, PlayerKind.Human);
        await _gameController.StartAsync(new NewGameOptions(ImmutableArray.Create(PlayerKind.Human)));
        
        var staleToken = new ViewToken(_gameController.View.Token.SessionId, 0); 
        var result = await _gameController.ChooseColorAsync(staleToken, GameColor.Blue);

        Assert.Equal(CommandError.StaleView, result.Error);
    }
    
    [Fact]
    public async Task ChooseColorAsync_NotHumanTurn_ReturnsNotAllowed()
    {
        _gameRules.NextState = CreateDummyGameState(GamePhase.Running, PlayerKind.Computer, PlayerKind.Human);
        await _gameController.StartAsync(new NewGameOptions(ImmutableArray.Create(PlayerKind.Computer, PlayerKind.Human)));

        var result = await _gameController.ChooseColorAsync(_gameController.View.Token, GameColor.Blue);

        Assert.Equal(CommandError.NotAllowed, result.Error);
    }
    
    [Fact]
    public async Task ChooseColorAsync_IllegalColor_ReturnsIllegalColor()
    {
        _gameRules.NextState = CreateDummyGameState(GamePhase.Running, PlayerKind.Human);
        await _gameController.StartAsync(new NewGameOptions(ImmutableArray.Create(PlayerKind.Human)));
        
        _gameRules.NextError = RuleError.IllegalColor;

        var result = await _gameController.ChooseColorAsync(_gameController.View.Token, GameColor.Red);

        Assert.Equal(CommandError.IllegalColor, result.Error);
    }

    [Fact]
    public async Task ChooseColorAsync_ValidHumanMove_IncrementsRevisionExactlyOnce()
    {
        _gameRules.NextState = CreateDummyGameState(GamePhase.Running, PlayerKind.Human);
        await _gameController.StartAsync(new NewGameOptions(ImmutableArray.Create(PlayerKind.Human, PlayerKind.Human)));
        var initialToken = _gameController.View.Token;
        
        var result = await _gameController.ChooseColorAsync(initialToken, GameColor.Green);
        
        Assert.Equal(CommandError.None, result.Error);
        Assert.Equal(initialToken.Revision + 1, _gameController.View.Token.Revision);
    }
    
    //SaveAsync tests
    [Fact]
    public async Task SaveAsync_StorageUnavailable_ReturnsMappedError()
    {
        _gameRules.NextState = CreateDummyGameState(GamePhase.Running, PlayerKind.Human);
        await _gameController.StartAsync(new NewGameOptions(ImmutableArray.Create(PlayerKind.Human)));
        
        _saveStore.NextSaveError = StoreError.Unavailable;

        var result = await _gameController.SaveAsync(_gameController.View.Token);

        Assert.Equal(CommandError.StorageUnavailable, result.Error);
    }
    
    [Fact]
    public async Task SaveAsync_IgnoredOldSessionCompletion_WhenResetOccurs()
    {
        _gameRules.NextState = CreateDummyGameState(GamePhase.Running, PlayerKind.Human);
        await _gameController.StartAsync(new NewGameOptions(ImmutableArray.Create(PlayerKind.Human)));
        
        var initialSessionId = _gameController.View.Token.SessionId;
        var saveTask = _gameController.SaveAsync(_gameController.View.Token);
        
        await _gameController.ReturnToSetupAsync();
        await saveTask;
        
        Assert.Equal(Screen.Setup, _gameController.View.Screen);
        Assert.NotEqual(initialSessionId, _gameController.View.Token.SessionId);
        Assert.False(_gameController.View.IsBusy);
    }
    
    //ResumeAsync tests
    [Fact]
    public async Task ResumeAsync_WithInvalidState_StaysInSetup()
    {
        _saveStore.NextLoadError = StoreError.None;
        await _saveStore.SaveAsync(new GameSnapshot(1, CreateDummyGameState(GamePhase.Running, PlayerKind.Human)), CancellationToken.None);
        
        _gameRules.IsValidResult = false;

        var result = await _gameController.ResumeAsync();

        Assert.Equal(CommandError.InvalidSave, result.Error);
        Assert.Equal(Screen.Setup, _gameController.View.Screen);
    }
    
    [Fact]
    public async Task ResumeAsync_AtComputerTurn_ReturnsInvalidSave()
    {
        _saveStore.NextLoadError = StoreError.None;
        await _saveStore.SaveAsync(new GameSnapshot(1, CreateDummyGameState(GamePhase.Running, PlayerKind.Computer)), CancellationToken.None);
        _gameRules.IsValidResult = true;

        var result = await _gameController.ResumeAsync();

        Assert.Equal(CommandError.InvalidSave, result.Error);
    }

    //ReturnToSetup tests
    [Fact]
    public async Task ReturnToSetup_CancelsPendingComputerTurn_AndIgnoresLateResult()
    {
        _gameRules.NextState = CreateDummyGameState(GamePhase.Running, PlayerKind.Computer);
        await _gameController.StartAsync(new NewGameOptions(ImmutableArray.Create(PlayerKind.Computer)));
        
        _computerStrategy.PauseEvent.Reset();
        await _gameController.ReturnToSetupAsync();
        _computerStrategy.PauseEvent.Set();
        await Task.Delay(100);
        
        Assert.Equal(Screen.Setup, _gameController.View.Screen);
    }
    
    //Events tests
    [Fact]
    public async Task ChangedEvent_FiresWhenViewUpdates()
    {
        var eventFiredCount = 0;
        _gameController.Changed += () => eventFiredCount++;
        
        _gameRules.NextState = CreateDummyGameState(GamePhase.Running, PlayerKind.Human);
        await _gameController.StartAsync(new NewGameOptions(ImmutableArray.Create(PlayerKind.Human)));

        Assert.True(eventFiredCount > 0);
    }
    
    //Computer tests
    [Fact]
    public async Task ComputerRunner_ConsecutiveTurns_EndAtHuman()
    {
        _gameRules.NextState = CreateDummyGameState(GamePhase.Running, PlayerKind.Computer, PlayerKind.Computer, PlayerKind.Human);
        
        var secondComputerState = CreateDummyGameState(GamePhase.Running, PlayerKind.Computer, PlayerKind.Computer, PlayerKind.Human);
        var stateWithCurrentPlayer1 = secondComputerState with { CurrentPlayerIndex = 1 };
        
        var humanState = CreateDummyGameState(GamePhase.Running, PlayerKind.Computer, PlayerKind.Computer, PlayerKind.Human);
        var stateWithCurrentPlayer2 = humanState with { CurrentPlayerIndex = 2 };

        int moveCount = 0;
        _gameRules.NextState = stateWithCurrentPlayer1;

        var tcs = new TaskCompletionSource<bool>();
        _gameController.Changed += () => 
        {
            var s = _gameController.View.State;
            if (s != null && s.Players[s.CurrentPlayerIndex].Kind == PlayerKind.Human)
            {
                tcs.TrySetResult(true);
            }
            
            if (moveCount == 1) _gameRules.NextState = stateWithCurrentPlayer2;
            moveCount++;
        };

        _computerStrategy.PauseEvent.Set();

        await _gameController.StartAsync(new NewGameOptions(ImmutableArray.Create(PlayerKind.Computer, PlayerKind.Computer, PlayerKind.Human)));
        
        await Task.WhenAny(tcs.Task, Task.Delay(2000));
        
        Assert.True(tcs.Task.IsCompleted);
        Assert.Equal(PlayerKind.Human, _gameController.View.State!.Players[_gameController.View.State.CurrentPlayerIndex].Kind);
    }
}