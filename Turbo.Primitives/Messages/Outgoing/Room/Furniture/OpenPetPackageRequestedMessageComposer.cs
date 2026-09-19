using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Outgoing.Room.Furniture;

/// <summary>Opens the naming dialog for a pet package; the figure previews what is inside.</summary>
[GenerateSerializer, Immutable]
public sealed record OpenPetPackageRequestedMessageComposer : IComposer
{
    [Id(0)]
    public required RoomObjectId ObjectId { get; init; }

    [Id(1)]
    public required PetFigureSnapshot? Figure { get; init; }
}
