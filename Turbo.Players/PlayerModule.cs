using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Turbo.Contracts.Plugins;
using Turbo.Players.Configuration;
using Turbo.Players.Providers;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Providers;

namespace Turbo.Players;

public sealed class PlayerModule : IHostPluginModule
{
    public string Key => "turbo-players";

    public void ConfigureServices(IServiceCollection services, HostApplicationBuilder builder)
    {
        services.Configure<PlayerConfig>(
            builder.Configuration.GetSection(PlayerConfig.SECTION_NAME)
        );

        services.AddSingleton<ICurrencyTypeProvider, CurrencyTypeProvider>();
        services.AddSingleton<IAchievementProvider, AchievementProvider>();
        services.AddSingleton<IPlayerService, PlayerService>();
    }
}
