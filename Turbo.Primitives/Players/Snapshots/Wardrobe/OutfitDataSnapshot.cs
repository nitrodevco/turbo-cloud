using Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Players.Snapshots.Wardrobe;

[GenerateSerializer, Immutable]
public sealed record OutfitDataSnapshot
{
    [Id(0)]
    public required int SlotId { get; init; }

    [Id(1)]
    public required string Figure { get; init; }

    [Id(2)]
    public required AvatarGenderType Gender { get; init; }
}
