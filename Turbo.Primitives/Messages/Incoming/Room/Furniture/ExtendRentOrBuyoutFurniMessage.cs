using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Room.Furniture;

/// <summary>
/// Extends the rent of, or buys out, a rented furni in the room (the infostand):
/// <c>ExtendRentOrBuyoutFurniMessageComposer(isWallItem, objectId, buyout)</c>.
/// </summary>
public record ExtendRentOrBuyoutFurniMessage : IMessageEvent
{
    public required bool IsWallItem { get; init; }

    public required int ObjectId { get; init; }

    public required bool Buyout { get; init; }
}
