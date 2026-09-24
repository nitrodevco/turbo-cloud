using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Turbo.Contracts.Plugins;
using Turbo.Guilds.Configuration;

namespace Turbo.Guilds;

public sealed class GuildModule : IHostPluginModule
{
    public string Key => "turbo-guilds";

    public void ConfigureServices(IServiceCollection services, HostApplicationBuilder builder)
    {
        services.Configure<GuildConfig>(builder.Configuration.GetSection(GuildConfig.SECTION_NAME));
    }
}
