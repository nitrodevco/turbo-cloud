using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.WiredTrading.Enums;

namespace Turbo.Primitives.WiredTrading.Grains;

/// <summary>
/// A player's trade with wired, keyed by player id: the window in which they offer inventory
/// items to a chest. One at a time; a new one replaces the old. Offers hold inventory
/// snapshots and reserve nothing, so the commit re-checks everything.
/// <para>
/// The room only tells this grain (it is awaited by nothing the room awaits); this grain
/// awaits the inventory and the room, which carries the deposit out.
/// </para>
/// </summary>
public interface IWiredTradeGrain : IGrainWithIntegerKey
{
    /// <summary>Opens the trade window to put things into a chest in this room.</summary>
    public Task StartChestDepositAsync(
        RoomId roomId,
        RoomObjectId chestId,
        WiredChestKind kind,
        CancellationToken ct
    );

    /// <summary>
    /// Opens the trade window for a contract's payment or trade (Initiate Transaction). A
    /// player already trading is refused, unless wired just cancelled that trade, which this
    /// one then replaces without the window closing.
    /// </summary>
    public Task StartContractAsync(WiredContractTradeRequest request, CancellationToken ct);

    /// <summary>
    /// Wired cancels the player's contract trade (Cancel Transaction), if it is for one of these
    /// contracts, or any when none are named. It ends after a short grace period.
    /// </summary>
    public Task CancelByWiredAsync(ImmutableArray<RoomObjectId> sourceIds, CancellationToken ct);

    public Task AddItemsAsync(ImmutableArray<RoomObjectId> itemIds, CancellationToken ct);

    public Task RemoveItemsAsync(ImmutableArray<RoomObjectId> itemIds, CancellationToken ct);

    /// <summary>
    /// The first press accepts the offer; the second, after the client's countdown, carries the
    /// trade out. Any change to the offer takes the acceptance back.
    /// </summary>
    public Task ConfirmAsync(bool isFinalConfirm, CancellationToken ct);

    /// <summary>Ends the trade, telling the client why.</summary>
    public Task CancelAsync(WiredTransactionFailureType reason, CancellationToken ct);
}
