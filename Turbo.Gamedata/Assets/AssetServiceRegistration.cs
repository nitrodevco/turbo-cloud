using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Turbo.Gamedata.Assets.Publishing;
using Turbo.Gamedata.Configuration;
using Turbo.Primitives.Gamedata;

namespace Turbo.Gamedata.Assets;

internal static class AssetServiceRegistration
{
    internal static IServiceCollection AddAssetBundleConfiguration(
        this IServiceCollection services,
        HostApplicationBuilder builder
    )
    {
        services.Configure<AssetBundleConfig>(
            builder.Configuration.GetSection(AssetBundleConfig.SECTION_NAME)
        );

        return services;
    }

    internal static IServiceCollection AddCloudflarePurgeClient(this IServiceCollection services)
    {
        services.AddHttpClient(
            CloudflarePurge.HTTP_CLIENT,
            (sp, http) =>
                http.Timeout = TimeSpan.FromSeconds(
                    Math.Max(
                        1,
                        sp.GetRequiredService<
                            IOptions<AssetBundleConfig>
                        >().Value.PublishTimeoutSeconds
                    )
                )
        );

        return services;
    }

    internal static IServiceCollection AddAssetBundleServices(this IServiceCollection services)
    {
        services.AddSingleton<IAssetBundleStore, AssetBundleStore>();
        services.AddSingleton<AssetJobs>();
        services.AddSingleton<IAssetJobs>(sp => sp.GetRequiredService<AssetJobs>());
        services.AddSingleton<AssetBundleChecks>();
        services.AddSingleton<IAssetBundleService, AssetBundleService>();
        services.AddSingleton<AssetDiscoveryCatalog>();
        services.AddHostedService(sp => sp.GetRequiredService<AssetDiscoveryCatalog>());
        // Stop jobs before their discovery catalog; transfers and final saves outlive discovery.
        services.AddHostedService(sp => sp.GetRequiredService<AssetJobs>());
        services.AddSingleton<IAssetSyncService, AssetSyncService>();

        return services;
    }

    internal static IServiceCollection AddAssetPublishingServices(this IServiceCollection services)
    {
        services.AddSingleton<AssetPasswordSealer>();
        services.AddSingleton<PublishConnections>();
        services.AddSingleton<CloudflarePurge>();
        services.AddSingleton<PublishedPets>();
        services.AddSingleton<PublishedFurnitureIcons>();
        services.AddSingleton<IAssetPublishService, AssetPublishService>();

        return services;
    }
}
