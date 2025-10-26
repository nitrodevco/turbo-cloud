using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;

namespace Turbo.Players;

public sealed class PlayerModule : IHostPluginModule
{
    public string Key => "turbo-players";

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<PlayerManager>();
    }
}
