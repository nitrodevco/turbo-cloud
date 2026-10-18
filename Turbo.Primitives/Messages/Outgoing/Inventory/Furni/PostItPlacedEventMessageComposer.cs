using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Furni;

/// <summary>
/// One sheet of a post-it stack was stuck to a wall; the stack stays in the inventory with the
/// sheets left as its legacy stuff data (Flash <c>FurniModel.updatePostItCount</c>).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record PostItPlacedEventMessageComposer : IComposer
{
    [Id(0)]
    public required RoomObjectId ItemId { get; init; }

    [Id(1)]
    public required int ItemsLeft { get; init; }
}
