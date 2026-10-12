using Orleans;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

/// <summary>
/// Asks the player to confirm a pickup before the room does it (Flash
/// <c>RoomMessageHandler.onObjectRemoveConfirm</c>). The client shows the two texts in a confirm
/// dialog and, on OK, sends the pickup again with its confirm flag set; cancel sends nothing.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record ObjectRemoveConfirmMessageComposer : IComposer
{
    [Id(0)]
    public required RoomItemCategoryType Category { get; init; }

    [Id(1)]
    public required RoomObjectId ObjectId { get; init; }

    [Id(2)]
    public required FurniturePickupConfirmation Confirmation { get; init; }
}
