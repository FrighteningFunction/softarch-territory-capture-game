using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using TerritoryCapture.Contracts;
using TerritoryCapture.TestSupport;

namespace TerritoryCapture.Infrastructure.Tests;

public class BrowserGameSaveStoreTests
{
    private readonly StorageRuntime _js = new();
    private readonly GameSnapshotJsonCodec _codec = new();
    private BrowserGameSaveStore Store => new(_js, _codec);

    [Fact]
    public async Task SaveOverwritesOneKeyAndLoadRestoresEveryField()
    {
        var first = new GameSnapshot(1, GameFixtures.HumanTurn());
        var second = new GameSnapshot(1, GameFixtures.MultiHop() with { TurnNumber = 12, CurrentPlayerIndex = 1 });
        Assert.Equal(StoreError.None, (await Store.SaveAsync(first, default)).Error);
        Assert.Equal(StoreError.None, (await Store.SaveAsync(second, default)).Error);
        var loaded = await Store.LoadAsync(default);
        Assert.Equal(StoreError.None, loaded.Error);
        Assert.Equal(_codec.Encode(second), _codec.Encode(loaded.Snapshot!));
        Assert.Equal(new[] { "localStorage.setItem", "localStorage.setItem", "localStorage.getItem" }, _js.Calls);
        Assert.Single(_js.Values);
    }

    [Fact]
    public async Task MissingSaveIsDistinctFromCorruptionAndUnsupportedVersion()
    {
        Assert.Equal(new LoadResult(null, StoreError.Missing), await Store.LoadAsync(default));
        _js.Values[GameSnapshotJsonCodec.StorageKey] = "broken";
        Assert.Equal(new LoadResult(null, StoreError.InvalidData), await Store.LoadAsync(default));
        _js.Values[GameSnapshotJsonCodec.StorageKey] = "{\"version\":2}";
        Assert.Equal(new LoadResult(null, StoreError.UnsupportedVersion), await Store.LoadAsync(default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QuotaOrUnavailableRuntimeDoesNotReplacePreviousSave(bool disposed)
    {
        await Store.SaveAsync(new(1, GameFixtures.HumanTurn()), default);
        var original = _js.Values[GameSnapshotJsonCodec.StorageKey];
        _js.Failure = disposed ? new ObjectDisposedException("runtime") : new JSException("QuotaExceededError");
        Assert.Equal(StoreError.Unavailable, (await Store.SaveAsync(new(1, GameFixtures.MultiHop()), default)).Error);
        Assert.Equal(original, _js.Values[GameSnapshotJsonCodec.StorageKey]);
        Assert.Equal(new LoadResult(null, StoreError.Unavailable), await Store.LoadAsync(default));
    }

    [Fact]
    public async Task InvalidSnapshotNeverCallsJsOrChangesStoredData()
    {
        await Store.SaveAsync(new(1, GameFixtures.HumanTurn()), default);
        var original = _js.Values[GameSnapshotJsonCodec.StorageKey];
        _js.Calls.Clear();
        Assert.Equal(StoreError.InvalidData, (await Store.SaveAsync(new(2, GameFixtures.MultiHop()), default)).Error);
        Assert.Empty(_js.Calls);
        Assert.Equal(original, _js.Values[GameSnapshotJsonCodec.StorageKey]);
    }

    [Fact]
    public async Task PreCanceledOperationsNeverCallJs()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Store.SaveAsync(
            new(1, GameFixtures.HumanTurn()), cancellation.Token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Store.LoadAsync(cancellation.Token).AsTask());
        Assert.Empty(_js.Calls);
    }

    [Fact]
    public async Task CancellationDuringLoadNeverReturnsACandidate()
    {
        await Store.SaveAsync(new(1, GameFixtures.HumanTurn()), default);
        using var cancellation = new CancellationTokenSource();
        _js.BeforeReturn = cancellation.Cancel;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Store.LoadAsync(cancellation.Token).AsTask());
        Assert.Equal(cancellation.Token, _js.LastToken);
    }

    [Fact]
    public async Task CanceledWriteMayHaveCommittedButNeverReportsSuccess()
    {
        using var cancellation = new CancellationTokenSource();
        _js.BeforeReturn = cancellation.Cancel;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Store.SaveAsync(
            new(1, GameFixtures.HumanTurn()), cancellation.Token).AsTask());
        Assert.Single(_js.Values); // Cancellation cannot roll back an atomic JS write.
    }

    [Fact]
    public void RegistrationIsIdempotentAndNeedsOnlyTheHostsJsRuntime()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(_js);
        services.AddTerritoryCaptureInfrastructure().AddTerritoryCaptureInfrastructure();
        Assert.Single(services, entry => entry.ServiceType == typeof(IGameSaveStore));
        Assert.Contains(services, entry => entry.ServiceType == typeof(IGameSaveStore) && entry.Lifetime == ServiceLifetime.Scoped);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.IsType<BrowserGameSaveStore>(scope.ServiceProvider.GetRequiredService<IGameSaveStore>());
        Assert.IsType<GreedyComputerStrategy>(provider.GetRequiredService<IComputerStrategy>());
    }

    private sealed class StorageRuntime : IJSRuntime
    {
        public Dictionary<string, string> Values { get; } = [];
        public List<string> Calls { get; } = [];
        public Exception? Failure { get; set; }
        public Action? BeforeReturn { get; set; }
        public CancellationToken LastToken { get; private set; }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, default, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            LastToken = cancellationToken;
            Calls.Add(identifier);
            if (Failure is not null) return ValueTask.FromException<TValue>(Failure);
            var key = Assert.IsType<string>(args![0]);
            Assert.Equal(GameSnapshotJsonCodec.StorageKey, key);
            object? result = null;
            if (identifier == "localStorage.setItem") Values[key] = Assert.IsType<string>(args[1]);
            else if (identifier == "localStorage.getItem") result = Values.GetValueOrDefault(key);
            else throw new InvalidOperationException($"Unexpected JS operation: {identifier}");
            BeforeReturn?.Invoke();
            return ValueTask.FromResult(result is null ? default! : (TValue)result);
        }
    }
}
