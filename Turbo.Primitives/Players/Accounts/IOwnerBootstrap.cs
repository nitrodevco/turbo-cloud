using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Players.Accounts;

/// <summary>
/// Who becomes the hotel's owner without anyone having to type console commands: the player the
/// hotel's configuration names (<c>Turbo:Owner</c>), by name or by Discord account, or, in
/// development only and with nothing configured, the first player there is. Whoever calls this
/// reports a new player or a new Discord link; the answer is whether that player is the owner.
/// It never throws: a failure is logged, and the sign-up it came with carries on.
/// </summary>
public interface IOwnerBootstrap
{
    /// <summary>A player was just created, by whatever way.</summary>
    Task<bool> PlayerCreatedAsync(PlayerId player, string name, CancellationToken ct);

    /// <summary>A Discord account was just linked to a player.</summary>
    Task<bool> DiscordLinkedAsync(PlayerId player, string discordId, CancellationToken ct);
}
