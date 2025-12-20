using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Furni;

[GenerateSerializer, Immutable]
public sealed record FurniListRemoveEventMessageComposer : IComposer
{
    [Id(0)]
    public required RoomObjectId ItemId { get; init; }
}
