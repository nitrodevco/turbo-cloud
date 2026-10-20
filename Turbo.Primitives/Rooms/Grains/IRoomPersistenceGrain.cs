using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Orleans.Concurrency;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Commands.Snapshots;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Chat;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Primitives.Rooms.Grains;

/// <summary>
/// The write buffer of one room. The <c>Enqueue*</c> methods are interleaved: they only put
/// rows in a buffer, in one synchronous stretch, and hold nothing across an await, so the room
/// that awaits them never waits for a database write the flush timer has under way. They stay
/// awaited rather than told, because the order a room hands things over in matters (a pickup
/// after a move must not be overtaken by the move) and Orleans only keeps the order of calls
/// that are awaited one after the other.
/// </summary>
public partial interface IRoomPersistenceGrain : IGrainWithIntegerKey
{
    /// <summary>Writes the chat lines, oldest first, on the next chatlog flush.</summary>
    [AlwaysInterleave]
    public Task EnqueueChatlogsAsync(List<RoomChatlogSnapshot> snapshots, CancellationToken ct);

    /// <summary>Writes the command uses, oldest first, on the next chatlog flush.</summary>
    [AlwaysInterleave]
    public Task EnqueueCommandLogsAsync(List<CommandLogSnapshot> snapshots, CancellationToken ct);

    /// <summary>Writes where the item stands and its data on the next flush.</summary>
    [AlwaysInterleave]
    public Task EnqueueDirtyItemAsync(
        RoomId roomId,
        RoomItemSnapshot snapshot,
        CancellationToken ct
    );

    /// <summary>
    /// Writes now, through the furniture custody, that <paramref name="item"/> left its owner's
    /// inventory for this room at the spot the snapshot gives, with its movement under
    /// <paramref name="cause"/>. Not interleaved: it waits for a flush under way, so no older
    /// write of this room lands after it. A write that fails stays queued, ahead of the next
    /// flush's updates.
    /// </summary>
    public Task WritePlacementAsync(
        RoomId roomId,
        RoomItemSnapshot item,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// Writes now, through the furniture custody, that <paramref name="items"/> left this room
    /// for the inventories their snapshots name, with the spot and data they left with and their
    /// movements under <paramref name="cause"/>; a queued update of one is dropped, so this room
    /// can no longer write it back into the room. Ordered and retried as
    /// <see cref="WritePlacementAsync"/>.
    /// </summary>
    public Task WritePickupAsync(
        RoomId roomId,
        List<RoomItemSnapshot> items,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// Writes every queued furni, pet and bot change now rather than on the next tick, for a room
    /// that is about to be read from the database again.
    /// </summary>
    public Task FlushNowAsync(CancellationToken ct);

    /// <summary>
    /// Gives borrowed furni that left the room back to the club on the next flush, in the order
    /// of the room's other changes of who holds what: their rows go, with their movements under
    /// <paramref name="cause"/>.
    /// </summary>
    [AlwaysInterleave]
    public Task EnqueueGiveBackAsync(
        RoomId roomId,
        List<RoomItemSnapshot> items,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// Writes a use now rather than on the next flush: the used-up item's row (none when
    /// <paramref name="usedUp"/> is null) is deleted, with its movement under its cause, through
    /// the furniture custody, and <paramref name="pets"/>, what the use
    /// changed, are written, in one transaction. What the use grants elsewhere (credits, an item
    /// given back) is written at once by its own grain, so a row left for the flush came back
    /// after a crash with the grant kept. When the write fails the pets stay queued for the next
    /// flush, but a used-up item is not: the call throws, its row stays, and the caller grants
    /// nothing.
    /// </summary>
    public Task WriteUseAsync(
        RoomId roomId,
        UsedUpItem? usedUp,
        List<PetSnapshot> pets,
        CancellationToken ct
    );

    /// <summary>
    /// Writes the row behind a newly borrowed furni, now rather than on the next flush: the room
    /// waits for it, because a borrow the database never heard of would vanish on the next room
    /// load while still counting against the borrower. False when the write failed, and the
    /// caller takes the furni back out of the room.
    /// </summary>
    public Task<bool> InsertBuildersClubItemAsync(
        RoomId roomId,
        RoomItemSnapshot snapshot,
        int offerId,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>Writes where items standing in the room stand and their data on the next flush.</summary>
    [AlwaysInterleave]
    public Task EnqueueDirtyItemsAsync(
        RoomId roomId,
        List<RoomItemSnapshot> snapshots,
        CancellationToken ct
    );

    /// <summary>Writes placed pets' positions and stats on the next flush.</summary>
    [AlwaysInterleave]
    public Task EnqueueDirtyPetsAsync(List<PetSnapshot> snapshots, CancellationToken ct);

    /// <summary>Writes placed bots' positions and settings on the next flush.</summary>
    [AlwaysInterleave]
    public Task EnqueueDirtyBotsAsync(List<BotSnapshot> snapshots, CancellationToken ct);
}
