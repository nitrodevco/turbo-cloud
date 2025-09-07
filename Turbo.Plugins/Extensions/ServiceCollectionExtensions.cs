using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Turbo.Plugins.Configuration;

namespace Turbo.Plugins.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection UseTurboPlugins(
        this IServiceCollection services,
        IConfiguration cfg
    )
    {
        services.AddOptions<PluginConfig>().Bind(cfg.GetSection(PluginConfig.SECTION_NAME));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<PluginConfig>>().Value);

        services.AddSingleton<PluginManager>();

        return services;
    }
}
