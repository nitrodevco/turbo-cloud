using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Room.Furniture;

/// <summary>
/// The recolor window's Apply: the furni, the scope and the colour's
/// channels.
/// </summary>
public record RecolorFurnitureMessage : IMessageEvent
{
    public required RoomObjectId ObjectId { get; init; }
    public required RecolorScopeType Scope { get; init; }
    public required int Red { get; init; }
    public required int Green { get; init; }
    public required int Blue { get; init; }
}
