using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Orleans.Concurrency;
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

    /// <summary>
    /// Counts a borrow of a furni with the given logic and tells the player their new total. A
    /// logic the club does not count (the stack helper tile) changes nothing and tells nobody.
    /// </summary>
    public Task OnBorrowedAsync(PlayerId playerId, string logicName, CancellationToken ct);

    /// <summary>
    /// Counts the returns of furni with these logics, one name per furni, and tells the player.
    /// A room that lets go of many at once (a deleted room) reports them together, so the player
    /// is told once; returns the club never counted are not reported at all.
    /// </summary>
    public Task OnReturnedAsync(
        PlayerId playerId,
        ImmutableArray<string> logicNames,
        CancellationToken ct
    );

    /// <summary>
    /// Told when a player's Builders Club membership changed, so the rooms holding what they
    /// borrowed are hidden or shown again at once rather than at the next sweep. Interleaved:
    /// it queries, and it touches none of the grain's state, so the placements asking for a
    /// count need not wait for it.
    /// </summary>
    [AlwaysInterleave]
    public Task OnSubscriptionChangedAsync(PlayerId playerId, CancellationToken ct);
}
