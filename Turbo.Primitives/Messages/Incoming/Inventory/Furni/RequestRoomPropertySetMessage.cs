using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Inventory.Furni;

/// <summary>Applies a room paper (wallpaper, floor or landscape) from the inventory to the room.</summary>
public record RequestRoomPropertySetMessage : IMessageEvent
{
    /// <summary>The paper's inventory item id.</summary>
    public required RoomObjectId ItemId { get; init; }
}
