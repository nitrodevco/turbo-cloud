using System.Collections.Immutable;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Crafting;

public record GetCraftingRecipesAvailableMessage : IMessageEvent
{
    public required RoomObjectId ObjectId { get; init; }
    public required ImmutableArray<RoomObjectId> ItemIds { get; init; }
}
