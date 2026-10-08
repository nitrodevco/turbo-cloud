using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Take the disk at a place in the room's jukebox's playlist out, back to its owner.</summary>
[GenerateSerializer, Immutable]
public sealed record RemoveJukeboxDiskInteraction : FurnitureInteraction
{
    [Id(0)]
    public required int Slot { get; init; }
}
