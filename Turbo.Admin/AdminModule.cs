using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Turbo.Admin.Auth;
using Turbo.Admin.Configuration;
using Turbo.Admin.Hosting;
using Turbo.Admin.Terminal;
using Turbo.Contracts.Plugins;

namespace Turbo.Admin;

public sealed class AdminModule : IHostPluginModule
{
    public string Key => "turbo-admin";

    public void ConfigureServices(IServiceCollection services, HostApplicationBuilder builder)
    {
        services.Configure<AdminConfig>(builder.Configuration.GetSection(AdminConfig.SECTION_NAME));

        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IAdminAccountService, AdminAccountService>();

        services.AddSingleton<ConsoleBroadcastService>();
        services.AddSingleton<IConsoleBroadcaster>(sp =>
            sp.GetRequiredService<ConsoleBroadcastService>()
        );

        services.AddSingleton<IAdminWebHost, AdminWebHost>();

        services.AddSingleton<ILoggerProvider, BroadcastLoggerProvider>();
    }
}
