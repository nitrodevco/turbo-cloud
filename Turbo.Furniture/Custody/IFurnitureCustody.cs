using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Furniture.Custody;

/// <summary>A furni to make: its definition and the extra data it starts with.</summary>
public readonly record struct FurniToMake(int DefinitionId, string? ExtraDataJson);

/// <summary>A furni row the custody wrote, as the inventory lists it.</summary>
public sealed record CustodyFurni(
    int Id,
    int DefinitionId,
    string? ExtraDataJson,
    DateTime CreatedAt
);

/// <summary>
/// The only writer of who holds a furni and where, on the inventory side (docs/movements.md "The
/// inventory"): every change of owner, place or existence is one explicit transition, written in
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

    /// <summary>None -> inventory: one row per furni, in order.</summary>
    Task<IReadOnlyList<CustodyFurni>> CreateInInventoryAsync(
        PlayerId owner,
        IReadOnlyList<FurniToMake> furni,
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
}
