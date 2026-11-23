using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TerritoryCapture.Infrastructure;
using TerritoryCapture.BrowserHarness;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.Services.AddTerritoryCaptureInfrastructure();
await builder.Build().RunAsync();
