using System;
using Turbo.Primitives.Movements.Enums;

namespace Turbo.Primitives.Movements;

/// <summary>
/// The movements of a pet or a bot (docs/movements.md "Pets and bots"). They are tracked for
/// custody only: no snapshot and no position. A pet's definition is its type; a bot has none.
/// </summary>
public static class UnitMovements
{
    /// <summary>The <paramref name="index"/>-th movement of <paramref name="cause"/>.</summary>
    public static AssetMovement Of(
        MovementCause cause,
        int index,
        AssetKind kind,
        int id,
        int definitionId,
        AssetPlace from,
        AssetPlace to,
        DateTime? at = null
    ) =>
        AssetMovement.For(
            cause,
            checked((short)(cause.FirstSeq + index)),
            at ?? DateTime.UtcNow,
            new AssetRef(kind, id, definitionId, null),
            from,
            to
        );

    public static AssetPlace Inventory(int ownerId) => new(ownerId, PlaceKind.Inventory, 0, null);

    public static AssetPlace InRoom(int ownerId, int roomId) =>
        new(ownerId, PlaceKind.Room, roomId, null);
}
