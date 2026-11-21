using TerritoryCapture.Contracts;
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
}
