using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TerritoryCapture.Web;
#if DEBUG
using TerritoryCapture.Contracts;
using TerritoryCapture.Web.Development;
#endif

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
#if DEBUG
builder.Services.AddScoped<IGameController, DemoController>();
#endif
// C: register real A/B/D services here when their implementations are ready.
await builder.Build().RunAsync();
