using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Turbo.Plugins.Configuration;

namespace Turbo.Plugins.Extensions;

public static class HostApplicationBuilderExtensions
{
    public static HostApplicationBuilder AddTurboPlugins(this HostApplicationBuilder builder)
    {
        builder.Services.Configure<PluginConfig>(
            builder.Configuration.GetSection(PluginConfig.SECTION_NAME)
        );

        builder.Services.AddSingleton<PluginManager>();
        builder.Services.AddHostedService<PluginsBootstrapper>();

        return builder;
    }
}
