using Orleans;
using Turbo.Primitives.Movements.Enums;

namespace Turbo.Primitives.Movements;

/// <summary>
/// Where an asset is, and whose it is there. Owner 0 is nobody: an asset that does not exist, a
/// borrowed furni, a chest's credits. <see cref="PlaceId"/> is the room, holder, offer or chest
/// <see cref="Kind"/> names (0 for an inventory or a wallet); <see cref="Position"/> is given in a
/// room only.
/// </summary>
[GenerateSerializer, Immutable]
public readonly record struct AssetPlace(
    [property: Id(0)] int OwnerPlayerId,
    [property: Id(1)] PlaceKind Kind,
    [property: Id(2)] long PlaceId,
    [property: Id(3)] RoomPosition? Position
)
{
    /// <summary>Where an asset comes from when it is created, and goes to when it is destroyed.</summary>
    public static AssetPlace Nowhere { get; } = new(0, PlaceKind.None, 0, null);
}
