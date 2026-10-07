using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.WiredTrading;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Primitives.Rooms.Grains;

/// <summary>
/// The room's side of wired chests that is not one chest's own packet: a confirmed trade's
/// deposit (called by <c>IWiredTradeGrain</c> only) and the wired menu's lock-all.
/// </summary>
public partial interface IRoomGrain
{
    /// <summary>
    /// Puts a player's offered inventory items into a chest standing in this room, if they may
    /// still put things in. The result says what moved, or why nothing did.
    /// </summary>
    public Task<WiredChestMoveResultSnapshot> DepositIntoWiredChestAsync(
        ActionContext ctx,
        RoomObjectId chestId,
        ImmutableArray<RoomObjectId> itemIds,
        CancellationToken ct
    );

    /// <summary>
    /// Carries out a payment or trade the player confirmed (called by <c>IWiredTradeGrain</c>
    /// only): the payment goes into the contract's chests, then its reward comes out of them.
    /// Null when it went through, otherwise why it did not; either way the transaction
    /// triggers hear of it.
    /// </summary>
    public Task<WiredTransactionFailureType?> CompleteWiredContractTradeAsync(
        ActionContext ctx,
        WiredContractTradeRequest request,
        ImmutableArray<FurnitureItemSnapshot> payment,
        int times,
        CancellationToken ct
    );

    /// <summary>
    /// A transaction wired offered ended without going through (the player cancelled, it timed
    /// out, wired cancelled it); the transaction triggers hear of it.
    /// </summary>
    public Task ReportWiredTransactionFailedAsync(
        PlayerId playerId,
        RoomObjectId sourceId,
        WiredTransactionFailureType reason,
        CancellationToken ct
    );

    /// <summary>
    /// Locks or unlocks the player's own chests here; with <paramref name="all"/>, the room's
    /// owner locks every chest in the room.
    /// </summary>
    public Task LockWiredChestsAsync(
        ActionContext ctx,
        bool locked,
        bool all,
        CancellationToken ct
    );

    /// <summary>
    /// Sends a page of transaction logs, of one chest here or of the whole room, to a player who
    /// may read the room's wired (or owns that chest). False when they may not.
    /// </summary>
    public Task<bool> RequestWiredTransactionLogsAsync(
        ActionContext ctx,
        RoomObjectId? chestId,
        int pageSize,
        int page,
        CancellationToken ct
    );

    /// <summary>Sends one of this room's transactions in full, to a player who may read its wired.</summary>
    public Task<bool> RequestWiredTransactionDetailsAsync(
        ActionContext ctx,
        long transactionId,
        CancellationToken ct
    );
}
