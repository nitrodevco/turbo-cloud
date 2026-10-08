using Orleans;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Put a song disk from the inventory into the room's jukebox, at a place in its playlist.</summary>
[GenerateSerializer, Immutable]
public sealed record AddJukeboxDiskInteraction : FurnitureInteraction
{
    [Id(0)]
    public required RoomObjectId DiskId { get; init; }

    /// <summary>Where in the playlist it goes; the client sends the playlist's length, the end.</summary>
    [Id(1)]
    public required int Slot { get; init; }
}
