using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Room.Furniture;

/// <summary>
/// The rent confirmation asks the price of renting, or buying out, a furni type by its furnidata
/// name: <c>GetRentOrBuyoutOfferMessageComposer(isWallItem, fullName, buyout)</c>.
/// </summary>
public record GetRentOrBuyoutOfferMessage : IMessageEvent
{
    public required bool IsWallItem { get; init; }

    public required string FurniTypeName { get; init; }

    public required bool Buyout { get; init; }
}
