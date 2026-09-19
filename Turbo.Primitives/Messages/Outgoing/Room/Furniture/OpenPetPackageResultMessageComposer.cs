using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Pets.Enums;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Outgoing.Room.Furniture;

[GenerateSerializer, Immutable]
public sealed record OpenPetPackageResultMessageComposer : IComposer
{
    [Id(0)]
    public required RoomObjectId ObjectId { get; init; }

    [Id(1)]
    public required PetNameValidationType NameValidationStatus { get; init; }

    [Id(2)]
    public required string NameValidationInfo { get; init; }
}
