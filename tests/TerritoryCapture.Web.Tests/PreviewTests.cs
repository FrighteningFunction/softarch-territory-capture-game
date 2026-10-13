using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TerritoryCapture.Contracts;
using TerritoryCapture.Web;
#if DEBUG
using TerritoryCapture.Web.Development;
#endif

namespace TerritoryCapture.Web.Tests;

public class PreviewTests
{
    [Fact]
    public void AppWithoutRegisteredControllerShowsHonestSkeleton()
    {
        using var context = new BunitContext();
        var app = context.Render<App>();
        Assert.Contains("Gameplay is awaiting implementation", app.Markup);
        Assert.Empty(app.FindAll("polygon"));
    }
#if !DEBUG
    [Fact]
    public void ReleaseAssemblyExcludesDevelopmentController()
    {
        Assert.Null(typeof(App).Assembly.GetType("TerritoryCapture.Web.Development.DemoController"));
    }
#endif
#if DEBUG
    [Fact]
    public void PreviewSwitchesBetweenBoardDrawAndSetup()
    {
        using var context = new BunitContext();
        context.Services.AddScoped<IGameController, DemoController>();
        var app = context.Render<App>();
        app.FindAll("button")[1].Click();
        Assert.Equal(120, app.FindAll("polygon").Count);
        Assert.Contains("Neutral cells: 118", app.Markup);
        app.FindAll("button")[3].Click();
        Assert.Contains("Draw: players 1, 2", app.Markup);
        Assert.Contains("Neutral cells: 0", app.Markup);
        app.FindAll("button")[0].Click();
        Assert.Empty(app.FindAll("polygon"));
    }
#endif
}
