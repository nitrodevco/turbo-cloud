using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Catalog.Grains;

/// <summary>
/// What the Builders Club knows about the whole hotel: how much furni each player has borrowed.
/// A player's borrows are spread across their rooms, so no room can answer this and no room is
/// asked to; rooms tell this grain what they did and read the count back from memory.
/// </summary>
public interface IBuildersClubGrain : IGrainWithStringKey
{
    /// <summary>How many furni this player is borrowing right now.</summary>
    public Task<int> GetBorrowedCountAsync(PlayerId playerId, CancellationToken ct);

    /// <summary>Sends the player their borrow count, which is what unlocks placement client-side.</summary>
    public Task SendFurniCountAsync(PlayerId playerId, CancellationToken ct);

    /// <summary>Counts a borrow and tells the player their new total.</summary>
    public Task OnBorrowedAsync(PlayerId playerId, CancellationToken ct);

    /// <summary>
    /// Counts <paramref name="count"/> returns and tells the player. A room that lets go of many
    /// at once (a deleted room) reports them together, so the player is told once.
    /// </summary>
    public Task OnReturnedAsync(PlayerId playerId, int count, CancellationToken ct);

    /// <summary>
    /// Told when a player's Builders Club membership changed, so the rooms holding what they
    /// borrowed are hidden or shown again at once rather than at the next sweep.
    /// </summary>
    public Task OnSubscriptionChangedAsync(PlayerId playerId, CancellationToken ct);
}
