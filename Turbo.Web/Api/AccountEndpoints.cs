using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Database.Context;
using Turbo.Primitives.Authentication;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Web.Accounts;
using Turbo.Web.Api.Contracts;
using Turbo.Web.Configuration;
using Turbo.Web.Sessions;

namespace Turbo.Web.Api;

/// <summary>
/// The site's account side: who is here, choosing a name to finish signing up, Play (a fresh
/// single-use login ticket in the client's address), and signing out.
/// </summary>
internal sealed class AccountEndpoints(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    WebAccounts accounts,
    WebSessions sessions,
    PendingSignUps signUps,
    ILoginTicketService tickets,
    ISanctionService sanctions,
    IOptions<WebConfig> config,
    ILogger<AccountEndpoints> logger
)
{
    private WebConfig Web => config.Value;

    public void Map(IEndpointRouteBuilder api)
    {
        api.MapGet(
            "/config",
            () =>
                Results.Ok(
                    new SiteConfigResponse(
                        Web.HotelName,
                        Web.RegistrationOpen,
                        Web.Discord.IsConfigured,
                        Web.ClientUrl.Contains("{ticket}", StringComparison.Ordinal)
                    )
                )
        );
        api.MapGet("/me", MeAsync);
        api.MapGet("/names/{name}", NameAsync);
        api.MapPost("/sign-up", SignUpAsync).RequireRateLimiting(WebApiServer.SIGN_IN_POLICY);
        api.MapPost("/play", PlayAsync);
        api.MapPost("/sign-out", SignOutAsync);
    }

    private async Task<IResult> MeAsync(HttpContext http, CancellationToken ct)
    {
        if (
            await sessions
                .FindAsync(WebCookies.Get(http, WebCookies.SESSION), ct)
                .ConfigureAwait(false) is
            { } player
        )
            return Results.Ok(
                new MeResponse(
                    await PlayerAsync(player, ct).ConfigureAwait(false),
                    await BanAsync(player, ct).ConfigureAwait(false),
                    null
                )
            );

        if (signUps.Find(WebCookies.Get(http, WebCookies.SIGN_UP)) is { } discord)
            return Results.Ok(
                new MeResponse(
                    null,
                    null,
                    new SignUpInfo(
                        discord.GlobalName ?? discord.Username,
                        await accounts.SuggestNameAsync(discord, ct).ConfigureAwait(false)
                    )
                )
            );

        return Results.Ok(new MeResponse(null, null, null));
    }

    private async Task<IResult> NameAsync(string name, CancellationToken ct)
    {
        if (PlayerNames.Check(name) is { } reason)
            return Results.Ok(new NameCheckResponse(false, reason));

        return await accounts.IsTakenAsync(name, ct).ConfigureAwait(false)
            ? Results.Ok(new NameCheckResponse(false, $"Someone is already called {name.Trim()}."))
            : Results.Ok(new NameCheckResponse(true, null));
    }

    private async Task<IResult> SignUpAsync(
        HttpContext http,
        SignUpRequest request,
        CancellationToken ct
    )
    {
        if (!Web.RegistrationOpen)
            return Refused(
                StatusCodes.Status403Forbidden,
                "New accounts aren't being made right now."
            );

        var token = WebCookies.Get(http, WebCookies.SIGN_UP);

        // Taken, so a double submit makes one player; put back if this one fails.
        if (signUps.Take(token) is not { } discord)
            return Refused(
                StatusCodes.Status401Unauthorized,
                "Your sign-up ran out. Sign in with Discord again."
            );

        var gender = string.Equals(request.Gender, "female", StringComparison.OrdinalIgnoreCase)
            ? AvatarGenderType.Female
            : AvatarGenderType.Male;
        var created = await accounts
            .CreateAsync(discord, request.Name ?? string.Empty, gender, ct)
            .ConfigureAwait(false);

        if (created.Created is not { } player)
        {
            signUps.Restore(token!, discord);

            return Refused(StatusCodes.Status400BadRequest, created.Error ?? "That didn't work.");
        }

        var (session, expires) = await sessions.StartAsync(player, ct).ConfigureAwait(false);

        WebCookies.Set(http, Web, WebCookies.SESSION, session, expires);
        WebCookies.Clear(http, WebCookies.SIGN_UP);

        return Results.Ok(
            new MeResponse(await PlayerAsync(player, ct).ConfigureAwait(false), null, null)
        );
    }

    private async Task<IResult> PlayAsync(HttpContext http, CancellationToken ct)
    {
        if (
            await sessions
                .FindAsync(WebCookies.Get(http, WebCookies.SESSION), ct)
                .ConfigureAwait(false)
            is not { } player
        )
            return Refused(StatusCodes.Status401Unauthorized, "Sign in first.");

        if (!Web.ClientUrl.Contains("{ticket}", StringComparison.Ordinal))
            return Refused(
                StatusCodes.Status503ServiceUnavailable,
                "The hotel isn't open to play from here yet."
            );

        if (await BanAsync(player, ct).ConfigureAwait(false) is { } ban)
            return Refused(
                StatusCodes.Status403Forbidden,
                ban.ExpiresAtUtc is { } until
                    ? $"You're banned until {until:u}: {ban.Reason}"
                    : $"You're banned: {ban.Reason}"
            );

        var ticket = await tickets
            .IssueAsync(player, TimeSpan.FromMinutes(Math.Max(1, Web.PlayTicketMinutes)), false, ct)
            .ConfigureAwait(false);

        logger.LogDebug("Issued player {PlayerId} a ticket to play from the site", player);

        return Results.Ok(
            new PlayResponse(
                Web.ClientUrl.Replace(
                    "{ticket}",
                    Uri.EscapeDataString(ticket.Ticket),
                    StringComparison.Ordinal
                )
            )
        );
    }

    private async Task<IResult> SignOutAsync(HttpContext http, CancellationToken ct)
    {
        await sessions.EndAsync(WebCookies.Get(http, WebCookies.SESSION), ct).ConfigureAwait(false);
        WebCookies.Clear(http, WebCookies.SESSION);
        WebCookies.Clear(http, WebCookies.SIGN_UP);

        return Results.NoContent();
    }

    private async Task<WebPlayer?> PlayerAsync(PlayerId player, CancellationToken ct)
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var row = await db
            .Players.AsNoTracking()
            .Where(x => x.Id == player.Value)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Motto,
                x.Figure,
            })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return row is null
            ? null
            : new WebPlayer(
                row.Id,
                row.Name,
                string.IsNullOrWhiteSpace(row.Motto) ? null : row.Motto,
                row.Figure,
                Web.AvatarImageUrl.Contains("{figure}", StringComparison.Ordinal)
                    ? Web.AvatarImageUrl.Replace(
                        "{figure}",
                        Uri.EscapeDataString(row.Figure),
                        StringComparison.Ordinal
                    )
                    : null
            );
    }

    private async Task<BanInfo?> BanAsync(PlayerId player, CancellationToken ct) =>
        await sanctions.GetActiveBanAsync(player, ct).ConfigureAwait(false) is { } ban
            ? new BanInfo(ban.Reason, ban.ExpiresAtUtc)
            : null;

    private static IResult Refused(int status, string message) =>
        Results.Json(new MessageResponse(message), statusCode: status);
}
