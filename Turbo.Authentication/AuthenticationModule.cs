using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Turbo.Contracts.Plugins;

namespace Turbo.Authentication;

public sealed class AuthenticationModule : IHostPluginModule
{
    public string Key => "turbo-authentication";

    public void ConfigureServices(IServiceCollection services, HostApplicationBuilder builder)
    {
        services.AddSingleton<AuthenticationService>();
    }
}
