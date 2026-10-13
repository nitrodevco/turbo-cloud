using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Crafting;

public record CraftMessage : IMessageEvent
{
    public required RoomObjectId ObjectId { get; init; }
    public required string RecipeCode { get; init; }
}
