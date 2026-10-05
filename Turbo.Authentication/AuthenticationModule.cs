using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Turbo.Contracts.Plugins;
using Turbo.Primitives.Authentication;

namespace Turbo.Authentication;

public sealed class AuthenticationModule : IHostPluginModule
{
    public string Key => "turbo-authentication";

    public void ConfigureServices(IServiceCollection services, HostApplicationBuilder builder)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<ILoginTicketService, LoginTicketService>();
    }
}
