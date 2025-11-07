using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Turbo.Contracts.Plugins;
using Turbo.Navigator.Abstractions;

namespace Turbo.Navigator;

public sealed class NavigatorModule : IHostPluginModule
{
    public string Key => "turbo-navigator";

    public void ConfigureServices(IServiceCollection services, HostApplicationBuilder builder)
    {
        services.AddSingleton<INavigatorService, NavigatorService>();
    }
}
