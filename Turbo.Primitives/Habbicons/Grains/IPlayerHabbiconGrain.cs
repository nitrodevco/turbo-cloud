using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Action;

namespace Turbo.Primitives.Habbicons.Grains;

/// <summary>
/// A player's Habbicons: which they have, which they favourited and used last, and buying,
/// claiming and showing them. Each answer is sent to the player by the grain.
/// </summary>
public interface IPlayerHabbiconGrain : IGrainWithIntegerKey
{
    /// <summary>The player's Habbicons, then the shop: what Habbo sends unasked at login.</summary>
    Task SendLoginAsync(CancellationToken ct);

    Task SendShopDataAsync(CancellationToken ct);

    /// <summary>One Habbicon with the player's state; nothing for an unknown id.</summary>
    Task SendInfoAsync(int habbiconId, CancellationToken ct);

    Task BuyAsync(int habbiconId, CancellationToken ct);

    /// <summary>Buys every Habbicon of the collection the player does not have, for the set's price.</summary>
    Task BuyCollectionAsync(int collectionId, CancellationToken ct);

    /// <summary>Takes a claimable Habbicon (a completed collection's reward).</summary>
    Task ClaimAsync(int habbiconId, CancellationToken ct);

    /// <summary>
    /// Gives a Habbicon for nothing (a reward track prize). True when the player has it
    /// afterwards, also when they had it already; false for an id no collection lists.
    /// </summary>
    Task<bool> GiveAsync(int habbiconId, CancellationToken ct);

    Task SetFavouriteAsync(int habbiconId, bool favourite, CancellationToken ct);

    /// <summary>
    /// Shows an owned Habbicon over the player's avatar in the room of <paramref name="ctx"/>.
    /// False when the player does not have it or is not in that room.
    /// </summary>
    Task<bool> TriggerAsync(ActionContext ctx, int habbiconId, CancellationToken ct);

    /// <summary>Whether the player has the Habbicon (owned or favourited) and can send it.</summary>
    Task<bool> CanUseAsync(int habbiconId, CancellationToken ct);

    /// <summary>Counts a use of an owned Habbicon towards the recent ones.</summary>
    Task NoteUsedAsync(int habbiconId, CancellationToken ct);
}
