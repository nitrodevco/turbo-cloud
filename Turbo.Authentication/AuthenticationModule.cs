using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;

namespace Turbo.Authentication;

public sealed class AuthenticationModule : IHostPluginModule
{
    public string Key => "turbo-authentication";

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<AuthenticationService>();
    }
}
