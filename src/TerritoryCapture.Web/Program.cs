using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TerritoryCapture.Web;
using TerritoryCapture.Contracts;
using TerritoryCapture.Application;
using TerritoryCapture.Rules;
using TerritoryCapture.Infrastructure;
#if DEBUG
using TerritoryCapture.Web.Development;
#endif

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
#if DEBUG
if (builder.HostEnvironment.IsDevelopment())
{
    builder.Services.AddScoped<IGameController, DemoController>();
}
else
#endif
{
    builder.Services.AddScoped<IGameRules, GameRules>();
    builder.Services.AddScoped<IComputerStrategy, GreedyComputerStrategy>();
    builder.Services.AddTerritoryCaptureInfrastructure();
    builder.Services.AddScoped<IGameController, GameController>();
}
await builder.Build().RunAsync();
