using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Turbo.Contracts.Plugins;
using Turbo.Players.Configuration;
using Turbo.Players.Permissions;
using Turbo.Players.Providers;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Providers;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Players;

public sealed class PlayerModule : IHostPluginModule
{
    public string Key => "turbo-players";

    public void ConfigureServices(IServiceCollection services, HostApplicationBuilder builder)
    {
        services.Configure<PlayerConfig>(
            builder.Configuration.GetSection(PlayerConfig.SECTION_NAME)
        );
        services.Configure<BadgeConfig>(builder.Configuration.GetSection(BadgeConfig.SECTION_NAME));
        services.Configure<PlayerNavigatorConfig>(
            builder.Configuration.GetSection(PlayerNavigatorConfig.SECTION_NAME)
        );

        services.AddSingleton<ICurrencyTypeProvider, CurrencyTypeProvider>();
        services.AddSingleton<IChatStyleProvider, ChatStyleProvider>();
        services.AddSingleton<IPlayerService, PlayerService>();
        // The clock the permission grains and console read expiry against; tests swap in a fake.
        // Orleans may register one already, so this only fills the gap.
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IPermissionRegistryProvider, PermissionRegistryProvider>();
        services.AddSingleton<IAssemblyFeatureProcessor, PermissionNodeFeatureProcessor>();
    }
}
