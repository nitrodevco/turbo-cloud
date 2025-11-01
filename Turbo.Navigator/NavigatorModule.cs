using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;
using Turbo.Navigator.Abstractions;

namespace Turbo.Navigator;

public sealed class NavigatorModule : IHostPluginModule
{
    public string Key => "turbo-navigator";

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<INavigatorService, NavigatorService>();
    }
}
