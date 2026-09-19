using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Pets.Enums;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Pets;

[GenerateSerializer, Immutable]
public sealed record ConfirmBreedingResultEventMessageComposer : IComposer
{
    [Id(0)]
    public required RoomObjectId NestId { get; init; }

    [Id(1)]
    public required ConfirmBreedingResultType Result { get; init; }
}
