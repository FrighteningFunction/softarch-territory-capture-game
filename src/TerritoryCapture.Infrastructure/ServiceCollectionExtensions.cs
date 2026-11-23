using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TerritoryCapture.Contracts;

namespace TerritoryCapture.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>Call from the Blazor composition root, which supplies IJSRuntime.</summary>
    public static IServiceCollection AddTerritoryCaptureInfrastructure(this IServiceCollection services)
    {
        services.TryAddSingleton<GameSnapshotJsonCodec>();
        services.TryAddSingleton<IComputerStrategy, GreedyComputerStrategy>();
        services.TryAddScoped<IGameSaveStore, BrowserGameSaveStore>();
        return services;
    }
}
