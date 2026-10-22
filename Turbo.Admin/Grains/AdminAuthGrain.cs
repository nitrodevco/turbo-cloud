using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Admin.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Entities.Admin;
using Turbo.Primitives.Admin.Enums;
using Turbo.Primitives.Admin.Grains;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Admin.Grains;

/// <summary>
/// Setup links, passkey ceremonies and panel sessions, by hash only (see
/// <see cref="IAdminAuthGrain"/>). Sessions are written through to <c>admin_sessions</c> and read
/// back on activation, so a restart signs nobody out; every request is answered from memory.
/// Kept alive: links and ceremonies live only here, and collecting it on idle would void them.
/// </summary>
[KeepAlive]
internal sealed class AdminAuthGrain(
    IDbContextFactory<TurboDbContext> database,
    IOptions<AdminConfig> config,
    TimeProvider timeProvider
) : Grain, IAdminAuthGrain
{
    private const int TOKEN_BYTES = 32;

    private readonly AdminConfig _config = config.Value;
    private readonly Dictionary<string, Pending> _setupTokens = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Pending> _ceremonies = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AdminSessionSnapshot> _sessions = new(
        StringComparer.Ordinal
    );

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        await using var db = await database.CreateDbContextAsync(ct);

        await db.AdminSessions.Where(x => x.ExpiresAt <= now).ExecuteDeleteAsync(ct);

        foreach (var row in await db.AdminSessions.AsNoTracking().ToListAsync(ct))
            _sessions[row.TokenHash] = ToSnapshot(row);
    }

    public Task<AdminSetupLinkSnapshot> CreateSetupTokenAsync(
        PlayerId playerId,
        CancellationToken ct
    )
    {
        var expiresAt = Purge().AddHours(_config.SetupLinkHours);

        return Task.FromResult(
            new AdminSetupLinkSnapshot
            {
                Token = Issue(_setupTokens, new Pending(playerId, expiresAt)),
                ExpiresAtUtc = expiresAt,
            }
        );
    }

    public Task<PlayerId?> GetSetupPlayerAsync(string setupToken, CancellationToken ct)
    {
        Purge();

        return Task.FromResult(
            !string.IsNullOrEmpty(setupToken)
            && _setupTokens.TryGetValue(Hash(setupToken), out var setup)
                ? setup.PlayerId
                : (PlayerId?)null
        );
    }

    public Task<PlayerId?> RedeemSetupTokenAsync(string setupToken, CancellationToken ct)
    {
        Purge();

        return Task.FromResult(Take(_setupTokens, setupToken)?.PlayerId);
    }

    public Task<string> BeginCeremonyAsync(
        PlayerId playerId,
        AdminCeremonyKind kind,
        string optionsJson,
        CancellationToken ct
    )
    {
        var now = Purge();

        return Task.FromResult(
            Issue(
                _ceremonies,
                new Pending(playerId, now.AddMinutes(_config.CeremonyMinutes), kind, optionsJson)
            )
        );
    }

    public Task<AdminCeremonySnapshot?> TakeCeremonyAsync(
        string ceremonyId,
        AdminCeremonyKind kind,
        CancellationToken ct
    )
    {
        Purge();

        // Taken whatever its kind: an id presented for the wrong step is not left to retry.
        var ceremony = Take(_ceremonies, ceremonyId);

        return Task.FromResult(
            ceremony is { Kind: var taken, OptionsJson: { } options } && taken == kind
                ? new AdminCeremonySnapshot { PlayerId = ceremony.PlayerId, OptionsJson = options }
                : null
        );
    }

    public async Task<AdminSessionGrant> OpenSessionAsync(PlayerId playerId, CancellationToken ct)
    {
        var now = Purge();

        // Past the limit, the player's oldest sessions end, as a new sign-in elsewhere would.
        var stale = _sessions
            .Where(x => x.Value.PlayerId == playerId)
            .OrderBy(x => x.Value.ExpiresAtUtc)
            .Select(x => x.Key)
            .SkipLast(Math.Max(0, _config.MaxSessionsPerPlayer - 1))
            .ToList();
        var token = NewToken();
        var row = new AdminSessionEntity
        {
            PlayerEntityId = playerId.Value,
            TokenHash = Hash(token),
            ExpiresAt = now.AddHours(_config.SessionHours),
            CreatedAt = now,
        };

        await using (var db = await database.CreateDbContextAsync(ct))
        {
            await db
                .AdminSessions.Where(x => x.ExpiresAt <= now || stale.Contains(x.TokenHash))
                .ExecuteDeleteAsync(ct);

            db.AdminSessions.Add(row);

            await db.SaveChangesAsync(ct);
        }

        foreach (var hash in stale)
            _sessions.Remove(hash);

        // The clock's time, not the column's: what was handed out is what is read back.
        var session = ToSnapshot(row) with
        {
            CreatedAtUtc = now,
        };

        _sessions[row.TokenHash] = session;

        return new AdminSessionGrant { SessionToken = token, Session = session };
    }

    public async Task<AdminSessionSnapshot?> GetSessionAsync(
        string sessionToken,
        CancellationToken ct
    )
    {
        if (string.IsNullOrEmpty(sessionToken))
            return null;

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var hash = Hash(sessionToken);

        if (!_sessions.TryGetValue(hash, out var session))
            return null;

        if (session.ExpiresAtUtc > now)
            return session;

        await EndAsync([hash], ct);

        return null;
    }

    public Task<ImmutableArray<AdminSessionSnapshot>> GetPlayerSessionsAsync(
        PlayerId playerId,
        CancellationToken ct
    )
    {
        var now = Purge();

        return Task.FromResult(
            _sessions
                .Values.Where(x => x.PlayerId == playerId && x.ExpiresAtUtc > now)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ThenByDescending(x => x.Id)
                .ToImmutableArray()
        );
    }

    public async Task EndSessionAsync(string sessionToken, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(sessionToken))
            await EndAsync([Hash(sessionToken)], ct);
    }

    public async Task<int> EndPlayerSessionsAsync(
        PlayerId playerId,
        int? sessionId,
        CancellationToken ct
    )
    {
        var hashes = _sessions
            .Where(x =>
                x.Value.PlayerId == playerId && (sessionId is not { } id || x.Value.Id == id)
            )
            .Select(x => x.Key)
            .ToList();

        await EndAsync(hashes, ct);

        return hashes.Count;
    }

    /// <summary>Ends sessions by hash, in the table first so a restart cannot bring one back.</summary>
    private async Task EndAsync(IReadOnlyCollection<string> hashes, CancellationToken ct)
    {
        if (hashes.Count == 0)
            return;

        await using (var db = await database.CreateDbContextAsync(ct))
            await db.AdminSessions.Where(x => hashes.Contains(x.TokenHash)).ExecuteDeleteAsync(ct);

        foreach (var hash in hashes)
            _sessions.Remove(hash);
    }

    /// <summary>A session row as the grain holds it; the database's times are UTC.</summary>
    private static AdminSessionSnapshot ToSnapshot(AdminSessionEntity row) =>
        new()
        {
            Id = row.Id,
            PlayerId = new PlayerId(row.PlayerEntityId),
            CreatedAtUtc = DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc),
            ExpiresAtUtc = DateTime.SpecifyKind(row.ExpiresAt, DateTimeKind.Utc),
        };

    /// <summary>Stores something under a new token's hash and hands out the token.</summary>
    private static string Issue(Dictionary<string, Pending> store, Pending pending)
    {
        var token = NewToken();

        store[Hash(token)] = pending;

        return token;
    }

    /// <summary>Removes and returns what a token stands for; null when it stands for nothing.</summary>
    private static Pending? Take(Dictionary<string, Pending> store, string token) =>
        !string.IsNullOrEmpty(token) && store.Remove(Hash(token), out var pending) ? pending : null;

    /// <summary>
    /// Drops everything expired from memory, and returns the time it went by. Expired session
    /// rows go at the next sign-in or start.
    /// </summary>
    private DateTime Purge()
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var store in (Dictionary<string, Pending>[])[_setupTokens, _ceremonies])
        {
            foreach (
                var key in store.Where(x => x.Value.ExpiresAtUtc <= now).Select(x => x.Key).ToList()
            )
                store.Remove(key);
        }

        foreach (
            var key in _sessions.Where(x => x.Value.ExpiresAtUtc <= now).Select(x => x.Key).ToList()
        )
            _sessions.Remove(key);

        return now;
    }

    private static string NewToken() =>
        Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TOKEN_BYTES));

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>A link or ceremony waiting to be used.</summary>
    private sealed record Pending(
        PlayerId PlayerId,
        DateTime ExpiresAtUtc,
        AdminCeremonyKind? Kind = null,
        string? OptionsJson = null
    );
}
