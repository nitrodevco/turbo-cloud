using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Authentication;

/// <summary>
/// A policy over a player asking to play: the site's Play, which hands out a login ticket, and the
/// client's login with one. It allows, or refuses with a reason the site or client shows and,
/// optionally, an address to send the player to (a page to vote on first, an appeal form). A
/// public class implementing it is found in a plugin's assembly as it loads and taken away as it
/// unloads. Policies run in <see cref="Order"/>, then by plugin key, then as registered; the first
/// refusal is the answer. With none the hotel lets everyone play (bans and maintenance are core's
/// own checks and come first). A policy that throws is logged and passed over: it allows.
/// See <c>docs/plugin-web-routes.md</c>.
/// </summary>
public interface IPlayPolicy
{
    /// <summary>Lower runs first. 0 by default.</summary>
    int Order => 0;

    ValueTask<PlayDecision> CheckAsync(PlayRequest request, CancellationToken ct);
}

/// <summary>Where a player asked to play from.</summary>
public enum PlaySource
{
    /// <summary>The site's Play, before a login ticket is issued.</summary>
    Site,

    /// <summary>The client's login with a ticket, before the session is the player's.</summary>
    Login,
}

/// <summary>
/// A player asking to play: who, from where, and the address the request came from (empty when
/// the server does not know it).
/// </summary>
public sealed record PlayRequest(PlayerId PlayerId, PlaySource Source, string RemoteAddress);

/// <summary>A policy's answer: allowed, or refused with a reason and an optional address to go to.</summary>
public sealed record PlayDecision(bool Allowed, string? Reason = null, string? RedirectUrl = null)
{
    public static readonly PlayDecision Allow = new(true);

    public static PlayDecision Refuse(string reason, string? redirectUrl = null) =>
        new(false, reason, redirectUrl);
}

/// <summary>The host's side of <see cref="IPlayPolicy"/>: every registered policy, run in order.</summary>
public interface IPlayPolicies
{
    ValueTask<PlayDecision> CheckAsync(PlayRequest request, CancellationToken ct);
}
