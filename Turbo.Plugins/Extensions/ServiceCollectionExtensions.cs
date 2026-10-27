using System;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Turbo.Contracts.Plugins;
using Turbo.Contracts.Plugins.Admin;
using Turbo.Contracts.Plugins.Mcp;
using Turbo.Contracts.Plugins.Web;
using Turbo.Plugins.Configuration;
using Turbo.Plugins.Data;
using Turbo.Plugins.Sources;
using Turbo.Primitives.Plugins;

namespace Turbo.Plugins.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTurboPlugins(
        this IServiceCollection services,
        HostApplicationBuilder builder
    )
    {
        var pluginSection = builder.Configuration.GetSection(PluginConfig.SECTION_NAME);

        services.Configure<PluginConfig>(pluginSection);

        services.AddSingleton<PluginManager>();
        services.AddSingleton<PluginAdminRoutes>();
        services.AddSingleton<IPluginAdminRoutes>(sp => sp.GetRequiredService<PluginAdminRoutes>());
        services.AddSingleton<PluginWebRoutes>();
        services.AddSingleton<IPluginWebRoutes>(sp => sp.GetRequiredService<PluginWebRoutes>());
        services.AddSingleton<McpTools>();
        services.AddSingleton<IMcpTools>(sp => sp.GetRequiredService<McpTools>());
        services.AddSingleton<PluginTables>();
        services.AddSingleton<IPluginDataOwner>(sp => sp.GetRequiredService<PluginTables>());
        services.AddSingleton<PluginAdministration>();
        services.AddSingleton<IPluginAdministration>(sp =>
            sp.GetRequiredService<PluginAdministration>()
        );
        services.AddSingleton<HotelDataPlugin>();
        services.AddSingleton<IHotelDataPlugin>(sp => sp.GetRequiredService<HotelDataPlugin>());
        services.AddSingleton<IThemeAuthoring, ThemeAuthoring>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IClientExtensionReports, ClientExtensionReports>();
        services.AddSingleton<PluginJobs>();
        services.AddSingleton<PluginSecrets>();
        services.AddSingleton(sp => new GitHubReleases(
            new HttpClient { Timeout = TimeSpan.FromMinutes(2) },
            sp.GetRequiredService<IOptions<PluginConfig>>()
        ));
        services.AddSingleton<PluginSources>();
        services.AddSingleton<IPluginSources>(sp => sp.GetRequiredService<PluginSources>());
        services.AddHostedService<PluginUpdateWatcher>();
        services.AddHostedService<PluginBootstrapper>();

        // Read through the bound class, not by key name: a string literal here would keep
        // working while the option was renamed, and it answers false for a missing section
        // instead of the option's own default.
        var pluginConfig = pluginSection.Get<PluginConfig>() ?? new PluginConfig();

        if (builder.Environment.IsDevelopment() && pluginConfig.HotReloadEnabled)
            services.AddHostedService<PluginHotReloadService>();

        return services;
    }

    public static IServiceCollection AddHostPlugin<TModule>(
        this IServiceCollection services,
        HostApplicationBuilder builder
    )
        where TModule : class, IHostPluginModule, new()
    {
        var module = new TModule();

        module.ConfigureServices(services, builder);

        services.AddSingleton<IHostPluginModule>(module);

        return services;
    }
}
