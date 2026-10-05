using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Turbo.Contracts.Plugins;
using Turbo.Web.Accounts;
using Turbo.Web.Api;
using Turbo.Web.Configuration;
using Turbo.Web.Discord;
using Turbo.Web.Sessions;

namespace Turbo.Web;

/// <summary>
/// The public site's server side: signing in with Discord, which makes a player the first time,
/// and Play, which opens the client with a fresh login ticket. Its own HTTP API beside the silo,
/// for the site (<c>turbo-web</c>) on its own domain. Off unless <c>Turbo:Web:Enabled</c>.
/// </summary>
public sealed class WebModule : IHostPluginModule
{
    public string Key => "turbo-web";

    public void ConfigureServices(IServiceCollection services, HostApplicationBuilder builder)
    {
        services.Configure<WebConfig>(builder.Configuration.GetSection(WebConfig.SECTION_NAME));

        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient<DiscordOAuthClient>(
            DiscordOAuthClient.HTTP_CLIENT,
            http => http.Timeout = TimeSpan.FromSeconds(15)
        );
        services.AddSingleton<WebAccounts>();
        services.AddSingleton<WebSessions>();
        services.AddSingleton<PendingSignUps>();
        services.AddHostedService<WebApiServer>();
    }
}
