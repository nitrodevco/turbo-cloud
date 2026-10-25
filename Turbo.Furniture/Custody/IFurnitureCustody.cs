using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Database.Context;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Furniture.Custody;

/// <summary>A furni to make: its definition and the extra data it starts with.</summary>
public readonly record struct FurniToMake(int DefinitionId, string? ExtraDataJson);

/// <summary>A furni to make straight into a room: what it is and where it stands.</summary>
public readonly record struct FurniToPlace(FurniToMake Furni, RoomPosition Position);

/// <summary>
/// A furni that holds others (a wired chest, a jukebox) and whom what it holds is kept for. A
/// wired chest also stamps each furni with the transaction that put it there.
/// </summary>
public readonly record struct FurniHolder(int ItemId, PlayerId HeldFor, long? TransactionId = null);

/// <summary>A furni row the custody wrote, as the inventory lists it.</summary>
public sealed record CustodyFurni(
    int Id,
    int DefinitionId,
    string? ExtraDataJson,
    DateTime CreatedAt
);

/// <summary>
/// The only writer of who holds a furni and where (docs/movements.md "The inventory", "The
/// room"): every change of owner, place or existence is one explicit transition, written in
/// one unit of work with its movements while recording. Each takes the caller's cause and numbers
/// its movements from <see cref="MovementCause.FirstSeq"/>, one per furni. The inventory keeps its
/// list in step; this writes the rows.
/// </summary>
public interface IFurnitureCustody
{
    /// <summary>
    /// Inventory -> none: the row of <paramref name="item"/>, held by <paramref name="owner"/> in
    /// no room and no holder, is deleted. False when there is no such row.
    /// </summary>
    Task<bool> DestroyInInventoryAsync(
        PlayerId owner,
        FurnitureItemSnapshot item,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// Inventory -> none for all of <paramref name="items"/> in one transaction, or for none: the
    /// ingredients of one craft. False when one is not there, and then nothing is deleted.
    /// </summary>
    Task<bool> DestroyInInventoryAsync(
        PlayerId owner,
        IReadOnlyList<FurnitureItemSnapshot> items,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// Inventory -> none, in the caller's unit of work (a chest that turns credit furni into its
    /// balance): the rows of all of <paramref name="items"/>, held by <paramref name="owner"/> in
    /// no room and no holder, are deleted. False when one is not there: the caller rolls back.
    /// </summary>
    Task<bool> DestroyInInventoryAsync(
        TurboDbContext dbCtx,
        PlayerId owner,
        IReadOnlyList<FurnitureItemSnapshot> items,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// No transition: the extra data of <paramref name="itemId"/>, held by <paramref name="owner"/>
    /// in no room and no holder, becomes <paramref name="extraDataJson"/> (a post-it pad's sheets
    /// left). Nothing changes hands, so nothing is recorded; the guard is the custody's, so a row
    /// that has left the inventory is not written. False when there is no such row.
    /// </summary>
    Task<bool> SetExtraDataInInventoryAsync(
        PlayerId owner,
        RoomObjectId itemId,
        string extraDataJson,
        CancellationToken ct
    );

    /// <summary>
    /// Inventory -> holder, in the caller's unit of work: all of <paramref name="items"/>, held by
    /// <paramref name="from"/> in no room and no holder, go into <paramref name="holder"/>, kept for
    /// its <see cref="FurniHolder.HeldFor"/>. False when one is not there: the caller rolls back.
    /// </summary>
    Task<bool> PutInHolderAsync(
        TurboDbContext dbCtx,
        PlayerId from,
        IReadOnlyList<FurnitureItemSnapshot> items,
        FurniHolder holder,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// Holder -> inventory, in the caller's unit of work: all of <paramref name="items"/> held by
    /// <paramref name="holder"/> go to <paramref name="to"/>. False when one is not held there:
    /// the caller rolls back.
    /// </summary>
    Task<bool> TakeOutOfHolderAsync(
        TurboDbContext dbCtx,
        FurniHolder holder,
        IReadOnlyList<FurnitureItemSnapshot> items,
        PlayerId to,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// None -> room: one row per furni, owned by <paramref name="owner"/> and standing in
    /// <paramref name="roomId"/> where it says, in order (a note pinned to a post-it wall).
    /// </summary>
    Task<IReadOnlyList<CustodyFurni>> CreateInRoomAsync(
        RoomId roomId,
        PlayerId owner,
        IReadOnlyList<FurniToPlace> furni,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// <see cref="CreateInRoomAsync(RoomId, PlayerId, IReadOnlyList{FurniToPlace}, MovementCause, CancellationToken)"/>
    /// in the caller's unit of work (a starter room painted and furnished in one write). The rows
    /// are saved, so their ids exist, when it answers; the caller commits.
    /// </summary>
    Task<IReadOnlyList<CustodyFurni>> CreateInRoomAsync(
        TurboDbContext dbCtx,
        RoomId roomId,
        PlayerId owner,
        IReadOnlyList<FurniToPlace> furni,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// Room -> inventory for every furni row still standing in <paramref name="roomId"/>, in the
    /// caller's unit of work: the backstop of a room deletion for a furni whose hand-over home
    /// failed. Answers how many there were.
    /// </summary>
    Task<int> SendHomeLeftInRoomAsync(
        TurboDbContext dbCtx,
        RoomId roomId,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// None -> room for a borrowed furni already standing in <paramref name="roomId"/>: its row in
    /// the Builders Club's own table. Nobody owns it, and it stands on its furniture alone: the
    /// offer it was lent from may go.
    /// </summary>
    Task BorrowIntoRoomAsync(
        RoomId roomId,
        RoomItemSnapshot item,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// Room -> none for borrowed furni, in the caller's unit of work: the rows of
    /// <paramref name="items"/> in <paramref name="roomId"/> go back to the club. A row already
    /// gone is skipped; answers how many went.
    /// </summary>
    Task<int> GiveBackBorrowedAsync(
        TurboDbContext dbCtx,
        RoomId roomId,
        IReadOnlyList<RoomItemSnapshot> items,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>None -> inventory: one row per furni, in order.</summary>
    Task<IReadOnlyList<CustodyFurni>> CreateInInventoryAsync(
        PlayerId owner,
        IReadOnlyList<FurniToMake> furni,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// None -> inventory, once per <paramref name="reference"/> for the owner: the row and its
    /// receipt (<c>furniture_grant_receipts</c>) in one transaction. Null when the owner was
    /// granted under that reference already: nothing is made.
    /// </summary>
    Task<CustodyFurni?> CreateInInventoryOnceAsync(
        PlayerId owner,
        FurniToMake furni,
        string reference,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// None -> inventory, two linked teleporters per definition: each row names the other in its
    /// <c>room_linker</c> section, and no half is ever left unlinked. Answers the rows pair by pair.
    /// </summary>
    Task<IReadOnlyList<CustodyFurni>> CreateTeleportPairsInInventoryAsync(
        PlayerId owner,
        IReadOnlyList<int> definitionIds,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// None -> inventory for the present and none -> holder (the present) for what it holds, both
    /// or neither. Answers the present; the held row stays out of every inventory until
    /// <see cref="TakeOutOfPresentAsync"/>.
    /// </summary>
    Task<CustodyFurni> CreatePresentInInventoryAsync(
        PlayerId owner,
        FurniToMake present,
        FurniToMake content,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// Holder -> inventory: what the present holds goes to <paramref name="opener"/>, whoever it
    /// was wrapped for (a present can change hands in a trade). Null when it holds nothing.
    /// </summary>
    Task<CustodyFurni?> TakeOutOfPresentAsync(
        PlayerId opener,
        RoomObjectId presentId,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// Inventory -> inventory: all of <paramref name="items"/>, held by <paramref name="from"/>
    /// in no room and no holder, go to <paramref name="to"/>, or none of them do.
    /// </summary>
    Task<bool> TransferBetweenInventoriesAsync(
        PlayerId from,
        PlayerId to,
        IReadOnlyList<FurnitureItemSnapshot> items,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// Inventory -> room: the row of <paramref name="item"/>, held by its owner in no room and no
    /// holder, stands in <paramref name="roomId"/> where the snapshot puts it. False when there is
    /// no such row.
    /// </summary>
    Task<bool> PlaceInRoomAsync(
        RoomId roomId,
        RoomItemSnapshot item,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// Room -> inventory: each of <paramref name="items"/> standing in <paramref name="roomId"/>
    /// goes to the inventory of the owner its snapshot names (the picker, when they keep it), with
    /// the spot and data it left with. A row no longer in the room is skipped; answers how many went.
    /// </summary>
    Task<int> TakeOutOfRoomAsync(
        RoomId roomId,
        IReadOnlyList<RoomItemSnapshot> items,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// Room -> none, in the caller's unit of work (a use that also writes the pets it changed):
    /// the row of <paramref name="item"/> standing in <paramref name="roomId"/> is deleted. False
    /// when there is no such row.
    /// </summary>
    Task<bool> DestroyInRoomAsync(
        TurboDbContext dbCtx,
        RoomId roomId,
        RoomItemSnapshot item,
        MovementCause cause,
        CancellationToken ct
    );
}
