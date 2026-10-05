using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Turbo.Web.Configuration;
using Turbo.Web.Discord;

namespace Turbo.Web.Sessions;

/// <summary>
/// Discord accounts that signed in with no player yet, waiting for their name, by a random token
/// in a cookie. In memory and short-lived: a restart only means signing in with Discord again.
/// Taking one ends it, so a sign-up is only made once.
/// </summary>
public sealed class PendingSignUps(IOptions<WebConfig> config, TimeProvider timeProvider)
{
    private const int TOKEN_BYTES = 24;

    private readonly ConcurrentDictionary<
        string,
        (DiscordUser User, DateTime ExpiresAtUtc)
    > _pending = new(StringComparer.Ordinal);

    public string Start(DiscordUser user)
    {
        Sweep();

        var token = WebSessions.Base64Url(RandomNumberGenerator.GetBytes(TOKEN_BYTES));

        _pending[token] = (
            user,
            timeProvider.GetUtcNow().UtcDateTime.AddMinutes(Math.Max(1, config.Value.SignUpMinutes))
        );

        return token;
    }

    /// <summary>Who is waiting under the token, still; null once it ended.</summary>
    public DiscordUser? Find(string? token) =>
        token is not null
        && _pending.TryGetValue(token, out var entry)
        && entry.ExpiresAtUtc > timeProvider.GetUtcNow().UtcDateTime
            ? entry.User
            : null;

    /// <summary>Who was waiting under the token, taken so no one else can use it; null if nobody.</summary>
    public DiscordUser? Take(string? token) =>
        token is not null
        && _pending.TryRemove(token, out var entry)
        && entry.ExpiresAtUtc > timeProvider.GetUtcNow().UtcDateTime
            ? entry.User
            : null;

    /// <summary>Puts a sign-up back after it failed, so the person can choose another name.</summary>
    public void Restore(string token, DiscordUser user) =>
        _pending[token] = (
            user,
            timeProvider.GetUtcNow().UtcDateTime.AddMinutes(Math.Max(1, config.Value.SignUpMinutes))
        );

    private void Sweep()
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var (token, entry) in _pending)
        {
            if (entry.ExpiresAtUtc <= now)
                _pending.TryRemove(token, out _);
        }
    }
}
