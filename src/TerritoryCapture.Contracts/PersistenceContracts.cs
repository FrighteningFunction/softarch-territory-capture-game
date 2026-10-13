using System.Collections.Immutable;

namespace TerritoryCapture.Contracts;

public interface IComputerStrategy {
    GameColor ChooseColor(GameState state, IGameRules rules);
}

public sealed record GameSnapshot(int Version, GameState State);
public enum StoreError {
    None, Missing, InvalidData, UnsupportedVersion, Unavailable
}
public sealed record StoreResult(StoreError Error);
public sealed record LoadResult(GameSnapshot? Snapshot, StoreError Error);

// Application-owned port; defined in Contracts for independent compilation.
public interface IGameSaveStore {
    ValueTask<StoreResult> SaveAsync(
        GameSnapshot snapshot, CancellationToken cancellationToken);
    ValueTask<LoadResult> LoadAsync(CancellationToken cancellationToken);
}
