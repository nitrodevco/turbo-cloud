using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Orleans.Concurrency;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Commands.Snapshots;
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
public interface IRoomPersistenceGrain : IGrainWithIntegerKey
{
    /// <summary>Writes the chat lines, oldest first, on the next chatlog flush.</summary>
    [AlwaysInterleave]
    public Task EnqueueChatlogsAsync(List<RoomChatlogSnapshot> snapshots, CancellationToken ct);

    /// <summary>Writes the command uses, oldest first, on the next chatlog flush.</summary>
    [AlwaysInterleave]
    public Task EnqueueCommandLogsAsync(List<CommandLogSnapshot> snapshots, CancellationToken ct);

    [AlwaysInterleave]
    public Task EnqueueDirtyItemAsync(
        RoomId roomId,
        RoomItemSnapshot snapshot,
        CancellationToken ct,
        bool remove = false
    );

    /// <summary>Deletes the item's row on the next flush instead of updating it.</summary>
    [AlwaysInterleave]
    public Task EnqueueDeletedItemAsync(RoomId roomId, RoomObjectId itemId, CancellationToken ct);

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
        CancellationToken ct
    );

    /// <summary>
    /// Writes items standing in the room on the next flush. Every item handed over here is in
    /// the room, so an earlier "taken out of the room" still queued for one of them is dropped.
    /// </summary>
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
