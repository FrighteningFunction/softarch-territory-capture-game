using System.Collections.Immutable;
using TerritoryCapture.Application;
using TerritoryCapture.Contracts;
using TerritoryCapture.Rules;
using TerritoryCapture.TestSupport;

namespace TerritoryCapture.Application.Tests;

public class FixtureTests
{
    [Fact]
    public async Task CanceledSaveDoesNotReplacePreviousSnapshot()
    {
        var store = new MemorySaveStore();
        var original = new GameSnapshot(1, GameFixtures.HumanTurn());
        await store.SaveAsync(original, CancellationToken.None);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await store.SaveAsync(new GameSnapshot(1, GameFixtures.Draw()), canceled.Token));
        var restored = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(StoreError.None, restored.Error);
        Assert.Same(original, restored.Snapshot);
    }

    [Fact]
    public async Task HumanMoveAdvancesExactlyOnceAndRejectsReusedViewToken()
    {
        using var controller = CreateController();
        await controller.StartAsync(HumanPlayers());
        var view = controller.View;
        var selectedColor = view.LegalColors[0];

        var accepted = await controller.ChooseColorAsync(view.Token, selectedColor);
        var repeated = await controller.ChooseColorAsync(view.Token, selectedColor);

        Assert.Equal(CommandError.None, accepted.Error);
        Assert.Equal(CommandError.StaleView, repeated.Error);
        Assert.Equal(1, controller.View.State!.TurnNumber);
        Assert.Equal(1, controller.View.State.CurrentPlayerIndex);
    }

    [Fact]
    public async Task ViewProjectionIncludesScoresAndOnlyEnablesHumanMoves()
    {
        using var controller = CreateController();
        await controller.StartAsync(HumanPlayers());

        var view = controller.View;

        Assert.Equal(Screen.Game, view.Screen);
        Assert.Equal(2, view.Players.Length);
        Assert.Equal(view.State!.Cells.Count(cell => cell.OwnerId == 0), view.Players[0].Score);
        Assert.Equal(view.State.Cells.Count(cell => cell.OwnerId == 1), view.Players[1].Score);
        Assert.NotEmpty(view.LegalColors);
        Assert.True(view.CanSave);
    }

    [Fact]
    public async Task SaveAndResumeRestoreValidatedStateAndResetPreservesSave()
    {
        var store = new MemorySaveStore();
        using var controller = CreateController(store);
        await controller.StartAsync(HumanPlayers());
        var originalState = controller.View.State;

        var saved = await controller.SaveAsync(controller.View.Token);
        await controller.ReturnToSetupAsync();
        var resumed = await controller.ResumeAsync();

        Assert.Equal(CommandError.None, saved.Error);
        Assert.Equal(CommandError.None, resumed.Error);
        Assert.Equal(Screen.Game, controller.View.Screen);
        Assert.Equal(originalState!.Cells.ToArray(), controller.View.State!.Cells.ToArray());
        Assert.Equal(originalState.TurnNumber, controller.View.State.TurnNumber);
    }

    [Fact]
    public async Task InvalidSaveRemainsInSetup()
    {
        var store = new MemorySaveStore();
        await store.SaveAsync(new GameSnapshot(1, GameFixtures.Draw()), CancellationToken.None);
        using var controller = CreateController(store);

        var result = await controller.ResumeAsync();

        Assert.Equal(CommandError.InvalidSave, result.Error);
        Assert.Equal(Screen.Setup, controller.View.Screen);
        Assert.Null(controller.View.State);
        Assert.False(controller.View.IsBusy);
    }

    [Fact]
    public async Task MissingSaveAdapterReportsUnavailableInsteadOfClaimingSuccess()
    {
        using var controller = new GameController(
            new GameRules(), new FirstLegalStrategy());
        await controller.StartAsync(HumanPlayers());

        var saveResult = await controller.SaveAsync(controller.View.Token);
        await controller.ReturnToSetupAsync();
        var resumeResult = await controller.ResumeAsync();

        Assert.Equal(CommandError.StorageUnavailable, saveResult.Error);
        Assert.Equal(CommandError.StorageUnavailable, resumeResult.Error);
        Assert.Equal(Screen.Setup, controller.View.Screen);
    }

    [Fact]
    public async Task LateLoadFromAnOldSessionCannotReplaceANewGame()
    {
        var store = new DelayedSaveStore();
        using var controller = CreateController(store);
        var pendingResume = controller.ResumeAsync().AsTask();
        await store.LoadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await controller.ReturnToSetupAsync();
        await controller.StartAsync(HumanPlayers());
        var currentState = controller.View.State;
        store.LoadCompletion.TrySetResult(
            new LoadResult(new GameSnapshot(1, GameFixtures.HumanTurn()), StoreError.None));

        var resumeResult = await pendingResume;

        Assert.Equal(CommandError.StaleView, resumeResult.Error);
        Assert.Same(currentState, controller.View.State);
        Assert.Equal(Screen.Game, controller.View.Screen);
    }

    [Fact]
    public async Task SaveIsRejectedWhileComputerTurnIsBusy()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ValueTask Pause(CancellationToken cancellationToken)
        {
            entered.TrySetResult();
            return new ValueTask(Task.Delay(Timeout.Infinite, cancellationToken));
        }

        using var controller = CreateController(yieldComputerTurn: Pause);
        await controller.StartAsync(new NewGameOptions(
            [PlayerKind.Computer, PlayerKind.Human]));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var result = await controller.SaveAsync(controller.View.Token);

        Assert.Equal(CommandError.Busy, result.Error);
        Assert.False(controller.View.CanSave);
        await controller.ReturnToSetupAsync();
    }

    [Fact]
    public async Task ReturnToSetupCancelsPendingComputerTurnAndDiscardsLateResult()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ValueTask Pause(CancellationToken cancellationToken)
        {
            entered.TrySetResult();
            return new ValueTask(release.Task.WaitAsync(cancellationToken));
        }

        using var controller = CreateController(yieldComputerTurn: Pause);
        await controller.StartAsync(new NewGameOptions(
            [PlayerKind.Computer, PlayerKind.Human]));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await controller.ReturnToSetupAsync();
        release.TrySetResult();

        Assert.Equal(Screen.Setup, controller.View.Screen);
        Assert.Null(controller.View.State);
    }

    [Fact]
    public async Task ConsecutiveComputerTurnsYieldAndStopAtNextHuman()
    {
        using var permits = new SemaphoreSlim(0);
        var controller = new GameController(
            new GameRules(),
            new FirstLegalStrategy(),
            new MemorySaveStore(),
            async cancellationToken => await permits.WaitAsync(cancellationToken));
        using (controller)
        {
            var firstMove = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            controller.Changed += () =>
            {
                if (controller.View.State?.TurnNumber == 1)
                {
                    firstMove.TrySetResult();
                }
                if (controller.View.State?.TurnNumber > 0
                    && !controller.View.IsBusy)
                {
                    finished.TrySetResult();
                }
            };
            await controller.StartAsync(new NewGameOptions(
                [PlayerKind.Computer, PlayerKind.Computer, PlayerKind.Human]));

            permits.Release();
            await firstMove.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(controller.View.IsBusy);
            Assert.Empty(controller.View.LegalColors);
            if (controller.View.IsBusy)
            {
                permits.Release();
            }
            await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(CommandError.None, controller.View.BackgroundError);
            Assert.False(controller.View.IsBusy);
            if (controller.View.State!.Phase == GamePhase.Running)
            {
                Assert.Equal(2, controller.View.State.CurrentPlayerIndex);
                Assert.NotEmpty(controller.View.LegalColors);
            }
            else
            {
                Assert.Equal(GamePhase.Finished, controller.View.State.Phase);
            }
        }
    }

    private static GameController CreateController(
        IGameSaveStore? store = null,
        Func<CancellationToken, ValueTask>? yieldComputerTurn = null) =>
        new(new GameRules(), new FixedStrategy(), store ?? new MemorySaveStore(),
            yieldComputerTurn);

    private static NewGameOptions HumanPlayers() =>
        new(ImmutableArray.Create(PlayerKind.Human, PlayerKind.Human));

    private sealed class FirstLegalStrategy : IComputerStrategy
    {
        public GameColor ChooseColor(GameState state, IGameRules rules) =>
            rules.LegalColors(state)[0];
    }

    private sealed class DelayedSaveStore : IGameSaveStore
    {
        public TaskCompletionSource LoadStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<LoadResult> LoadCompletion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask<StoreResult> SaveAsync(
            GameSnapshot snapshot, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new StoreResult(StoreError.None));

        public async ValueTask<LoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            LoadStarted.TrySetResult();
            return await LoadCompletion.Task.WaitAsync(cancellationToken);
        }
    }
}
