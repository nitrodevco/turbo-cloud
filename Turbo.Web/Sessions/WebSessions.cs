using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Turbo.Database.Context;
using Turbo.Database.Entities.Security;
using Turbo.Primitives.Players;
using Turbo.Web.Configuration;

namespace Turbo.Web.Sessions;

/// <summary>
/// Sign-ins to the public site: a random token for the browser's cookie, and its SHA-256 in
/// <c>web_sessions</c> with the player and when it ends.
/// </summary>
public sealed class WebSessions(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<WebConfig> config,
    TimeProvider timeProvider
)
{
    private const int TOKEN_BYTES = 32;

    /// <summary>A new sign-in for the player: the token for the cookie, and when it ends.</summary>
    public async Task<(string Token, DateTime ExpiresAtUtc)> StartAsync(
        PlayerId player,
        CancellationToken ct
    )
    {
        var token = Base64Url(RandomNumberGenerator.GetBytes(TOKEN_BYTES));
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expires = now.AddDays(Math.Max(1, config.Value.SessionDays));

        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        // Ended sign-ins of anyone go as a new one starts, so the table keeps only live ones.
        await db
            .WebSessions.Where(x => x.ExpiresAt <= now)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);

        db.WebSessions.Add(
            new WebSessionEntity
            {
                TokenHash = Hash(token),
                PlayerEntityId = player.Value,
                ExpiresAt = expires,
                PlayerEntity = null!,
            }
        );
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return (token, expires);
    }

    /// <summary>The player a token signs in, while it has not ended; null otherwise.</summary>
    public async Task<PlayerId?> FindAsync(string? token, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(token))
            return null;

        var hash = Hash(token);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var player = await db
            .WebSessions.AsNoTracking()
            .Where(x => x.TokenHash == hash && x.ExpiresAt > now)
            .Select(x => (int?)x.PlayerEntityId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return player is { } id ? id : null;
    }

    public async Task EndAsync(string? token, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(token))
            return;

        var hash = Hash(token);
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        await db
            .WebSessions.Where(x => x.TokenHash == hash)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);
    }

    private static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
