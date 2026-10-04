using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Admin.Configuration;
using Turbo.Primitives.Admin.Enums;
using Turbo.Primitives.Admin.Grains;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Admin.Grains;

/// <summary>
/// Setup links, passkey ceremonies and panel sessions, in memory and by hash only (see
/// <see cref="IAdminAuthGrain"/>). Kept alive: its whole state is the set of signed-in staff and
/// the links handed out, and collecting it on idle would void them all.
/// </summary>
[KeepAlive]
internal sealed class AdminAuthGrain(IOptions<AdminConfig> config, TimeProvider timeProvider)
    : Grain,
        IAdminAuthGrain
{
    private const int TOKEN_BYTES = 32;

    private readonly AdminConfig _config = config.Value;
    private readonly Dictionary<string, Pending> _setupTokens = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Pending> _ceremonies = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AdminSessionSnapshot> _sessions = new(
        StringComparer.Ordinal
    );

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

    public Task<AdminSessionGrant> OpenSessionAsync(PlayerId playerId, CancellationToken ct)
    {
        Purge();

        return Task.FromResult(Open(playerId));
    }

    public Task<AdminSessionSnapshot?> GetSessionAsync(string sessionToken, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(sessionToken))
            return Task.FromResult<AdminSessionSnapshot?>(null);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var hash = Hash(sessionToken);

        if (!_sessions.TryGetValue(hash, out var session))
            return Task.FromResult<AdminSessionSnapshot?>(null);

        if (session.ExpiresAtUtc > now)
            return Task.FromResult<AdminSessionSnapshot?>(session);

        _sessions.Remove(hash);

        return Task.FromResult<AdminSessionSnapshot?>(null);
    }

    public Task EndSessionAsync(string sessionToken, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(sessionToken))
            _sessions.Remove(Hash(sessionToken));

        return Task.CompletedTask;
    }

    private AdminSessionGrant Open(PlayerId playerId)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Past the limit, the player's oldest sessions end, as a new sign-in elsewhere would.
        foreach (
            var stale in _sessions
                .Where(x => x.Value.PlayerId == playerId)
                .OrderBy(x => x.Value.ExpiresAtUtc)
                .Select(x => x.Key)
                .SkipLast(Math.Max(0, _config.MaxSessionsPerPlayer - 1))
                .ToList()
        )
            _sessions.Remove(stale);

        var token = NewToken();
        var session = new AdminSessionSnapshot
        {
            PlayerId = playerId,
            ExpiresAtUtc = now.AddHours(_config.SessionHours),
        };

        _sessions[Hash(token)] = session;

        return new AdminSessionGrant { SessionToken = token, Session = session };
    }

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

    /// <summary>Drops everything expired, and returns the time it went by.</summary>
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
