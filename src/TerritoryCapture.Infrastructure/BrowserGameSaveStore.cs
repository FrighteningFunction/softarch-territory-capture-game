using Microsoft.JSInterop;
using TerritoryCapture.Contracts;

namespace TerritoryCapture.Infrastructure;

/// <summary>One atomic localStorage value; game validity/admission belongs to Application.</summary>
public sealed class BrowserGameSaveStore(IJSRuntime js, GameSnapshotJsonCodec codec) : IGameSaveStore
{
    public async ValueTask<StoreResult> SaveAsync(GameSnapshot snapshot, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string json;
        try
        {
            json = codec.Encode(snapshot);
        }
        catch (ArgumentException)
        {
            return new(StoreError.InvalidData);
        }

        try
        {
            // Serialize first. Never remove the previous save before replacing it.
            await js.InvokeVoidAsync("localStorage.setItem", cancellationToken,
                GameSnapshotJsonCodec.StorageKey, json);
            cancellationToken.ThrowIfCancellationRequested();
            return new(StoreError.None);
        }
        catch (JSException)
        {
            return new(StoreError.Unavailable);
        }
        catch (InvalidOperationException)
        {
            // Includes a disposed/unavailable JS runtime, e.g. before browser startup.
            return new(StoreError.Unavailable);
        }
    }

    public async ValueTask<LoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", cancellationToken,
                GameSnapshotJsonCodec.StorageKey);
            cancellationToken.ThrowIfCancellationRequested();
            return codec.Decode(json);
        }
        catch (JSException)
        {
            return new(null, StoreError.Unavailable);
        }
        catch (InvalidOperationException)
        {
            return new(null, StoreError.Unavailable);
        }
    }
}
