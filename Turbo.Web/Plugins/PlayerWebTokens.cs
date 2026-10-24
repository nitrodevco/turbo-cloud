using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Turbo.Primitives.Players;
using Turbo.Primitives.Web;
using Turbo.Web.Configuration;
using Turbo.Web.Sessions;

namespace Turbo.Web.Plugins;

/// <summary>
/// The clients' tokens for plugins' web routes (<see cref="IPlayerWebTokens"/>): a random token per
/// player, its SHA-256 kept in memory with when it runs out. A new one replaces the player's last,
/// so a client asking again and again holds one; a restart forgets them all, and a client asks
/// for another when a route answers 401.
/// </summary>
internal sealed class PlayerWebTokens(IOptions<WebConfig> config, TimeProvider timeProvider)
    : IPlayerWebTokens
{
    private const int TOKEN_BYTES = 32;

    private readonly ConcurrentDictionary<
        string,
        (PlayerId Player, DateTimeOffset Expires)
    > _byHash = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<PlayerId, string> _byPlayer = new();

    public PlayerWebToken? Issue(PlayerId player)
    {
        var web = config.Value;
        var routes = web.PluginRoutes;

        if (!web.Enabled || !routes.Enabled || player <= 0)
            return null;

        var minutes = Math.Max(1, routes.ClientTokenMinutes);
        var token = WebSessions.Base64Url(RandomNumberGenerator.GetBytes(TOKEN_BYTES));
        var hash = Hash(token);
        var now = timeProvider.GetUtcNow();

        _byHash[hash] = (player, now.AddMinutes(minutes));

        if (_byPlayer.TryGetValue(player, out var last) && last != hash)
            _byHash.TryRemove(last, out _);

        _byPlayer[player] = hash;

        return new PlayerWebToken(BaseUrl(web), token, minutes * 60);
    }

    public PlayerId? Find(string? token)
    {
        if (string.IsNullOrEmpty(token) || !_byHash.TryGetValue(Hash(token), out var entry))
            return null;

        if (entry.Expires > timeProvider.GetUtcNow())
            return entry.Player;

        _byHash.TryRemove(Hash(token), out _);

        return null;
    }

    /// <summary>Where clients call the routes: the setting, else the site whose /api is this host.</summary>
    public static string BaseUrl(WebConfig web) =>
        (
            string.IsNullOrWhiteSpace(web.PluginRoutes.PublicUrl)
                ? web.SiteUrl
                : web.PluginRoutes.PublicUrl
        ).TrimEnd('/');

    private static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
