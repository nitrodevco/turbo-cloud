using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Turbo.Web.Accounts;
using Turbo.Web.Configuration;
using Turbo.Web.Discord;
using Turbo.Web.Sessions;

namespace Turbo.Web.Api;

/// <summary>
/// Signing in with Discord. <c>/auth/discord</c> sends the browser to Discord with a random state,
/// also kept in a cookie; the callback checks the two match, so no other site can finish a sign-in
/// in someone's browser. A Discord account linked to a player is signed in; one that is not starts
/// a sign-up, which asks for a name; anything that goes wrong ends on the site with a reason.
/// </summary>
internal sealed class DiscordEndpoints(
    DiscordOAuthClient discord,
    WebAccounts accounts,
    WebSessions sessions,
    PendingSignUps signUps,
    IOptions<WebConfig> config
)
{
    private const int STATE_MINUTES = 10;

    private WebConfig Web => config.Value;

    public void Map(IEndpointRouteBuilder api)
    {
        api.MapGet("/auth/discord", Start).RequireRateLimiting(WebApiServer.SIGN_IN_POLICY);
        api.MapGet("/auth/discord/callback", CallbackAsync)
            .RequireRateLimiting(WebApiServer.SIGN_IN_POLICY);
    }

    private IResult Start(HttpContext http)
    {
        if (!Web.Discord.IsConfigured)
            return Back("unavailable");

        var state = WebSessions.Base64Url(RandomNumberGenerator.GetBytes(24));

        WebCookies.Set(
            http,
            Web,
            WebCookies.STATE,
            state,
            DateTimeOffset.UtcNow.AddMinutes(STATE_MINUTES)
        );

        return Results.Redirect(discord.AuthorizeUrl(state));
    }

    private async Task<IResult> CallbackAsync(
        HttpContext http,
        string? code,
        string? state,
        string? error,
        CancellationToken ct
    )
    {
        var expected = WebCookies.Get(http, WebCookies.STATE);

        WebCookies.Clear(http, WebCookies.STATE);

        if (error is not null)
            return Back("denied");

        if (
            string.IsNullOrEmpty(state)
            || expected is null
            || !CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(state),
                System.Text.Encoding.UTF8.GetBytes(expected)
            )
        )
            return Back("expired");

        if (
            string.IsNullOrEmpty(code)
            || await discord.GetUserAsync(code, ct).ConfigureAwait(false) is not { } user
        )
            return Back("discord");

        if (await accounts.FindAsync(user, ct).ConfigureAwait(false) is { } player)
        {
            var (token, expires) = await sessions.StartAsync(player, ct).ConfigureAwait(false);

            WebCookies.Set(http, Web, WebCookies.SESSION, token, expires);
            WebCookies.Clear(http, WebCookies.SIGN_UP);

            return Results.Redirect(Site("/"));
        }

        if (!Web.RegistrationOpen)
            return Back("closed");

        WebCookies.Set(
            http,
            Web,
            WebCookies.SIGN_UP,
            signUps.Start(user),
            DateTimeOffset.UtcNow.AddMinutes(Math.Max(1, Web.SignUpMinutes))
        );

        return Results.Redirect(Site("/welcome"));
    }

    private IResult Back(string reason) => Results.Redirect(Site($"/?error={reason}"));

    private string Site(string path) => Web.SiteUrl.TrimEnd('/') + path;
}
