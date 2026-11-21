using TerritoryCapture.Contracts;

namespace TerritoryCapture.TestSupport;

public class InMemorySaveStore : IGameSaveStore
{
    private GameSnapshot? _gameSnapshot;
    
    public StoreError NextSaveError { get; set; } =  StoreError.None;
    public StoreError NextLoadError { get; set; } =  StoreError.None;

    public ValueTask<StoreResult> SaveAsync(GameSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (NextSaveError == StoreError.None)
            _gameSnapshot = snapshot;
        return ValueTask.FromResult(new StoreResult(NextSaveError));
    }

    public ValueTask<LoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        if (NextLoadError != StoreError.None || _gameSnapshot == null)
            return ValueTask.FromResult(new LoadResult(null,
                NextLoadError != StoreError.None ? NextLoadError : StoreError.Missing));
        return ValueTask.FromResult(new LoadResult(_gameSnapshot, StoreError.None));
    }
}