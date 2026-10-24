using Turbo.Primitives.Players;

namespace Turbo.Primitives.Web;

/// <summary>
/// The tokens a signed-in client is given to call plugins' web routes as its player
/// (<c>web.session</c>, <c>docs/plugin-web-routes.md</c>): one per player at a time, a new one
/// replacing the last, kept in the server's memory until it runs out.
/// </summary>
public interface IPlayerWebTokens
{
    /// <summary>A new token for the player; null when the hotel serves no plugin routes.</summary>
    PlayerWebToken? Issue(PlayerId player);

    /// <summary>The player a token signs in, while it has not run out; null otherwise.</summary>
    PlayerId? Find(string? token);
}

/// <summary>
/// A token for plugins' web routes: the address before <c>/api/plugins/</c>, the token for the
/// <c>Authorization: Bearer</c> header, and how many seconds it lasts.
/// </summary>
public sealed record PlayerWebToken(string BaseUrl, string Token, int ExpiresInSeconds);
