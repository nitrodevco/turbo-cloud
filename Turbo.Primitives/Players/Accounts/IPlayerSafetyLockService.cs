using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Players.Accounts;

/// <summary>
/// Habbo's account safety lock. The client learns it at login (<c>UserObject.accountSafetyLocked</c>)
/// and from <c>AccountSafetyLockStatusChange</c> while online; locked, it turns off the catalogue's
/// buy and gift buttons, the marketplace's buy buttons, the inventory's sell button, trading and
/// room deletion, and shows the toolbar's unlock notice. How a player unlocks (Habbo: safety
/// questions on the website) is the hotel's: a web plugin calls <see cref="SetLockedAsync"/>.
/// </summary>
public interface IPlayerSafetyLockService
{
    /// <summary>Whether the player's account is safety locked; false for a player who does not exist.</summary>
    Task<bool> IsLockedAsync(PlayerId playerId, CancellationToken ct);

    /// <summary>
    /// Locks or unlocks the account and, when that changed it, tells the player's client if they
    /// are online. False when it was already so or there is no such player.
    /// </summary>
    Task<bool> SetLockedAsync(PlayerId playerId, bool locked, CancellationToken ct);
}
