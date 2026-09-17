using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Turbo.Contracts.Plugins;
using Turbo.Navigator.Configuration;
using Turbo.Primitives.Navigator;

namespace Turbo.Navigator;

public sealed class NavigatorModule : IHostPluginModule
{
    public string Key => "turbo-navigator";

    public void ConfigureServices(IServiceCollection services, HostApplicationBuilder builder)
    {
        services.Configure<NavigatorConfig>(
            builder.Configuration.GetSection(NavigatorConfig.SECTION_NAME)
        );

        services.AddSingleton<INavigatorService, NavigatorService>();
        services.AddSingleton<INavigatorProvider, NavigatorProvider>();
    }
}
