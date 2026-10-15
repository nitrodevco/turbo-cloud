using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Room.Furniture;

/// <summary>
/// Extends the rent of, or buys out, a rented furni in the inventory:
/// <c>ExtendRentOrBuyoutStripItemMessageComposer(stripItemId, buyout)</c>.
/// </summary>
public record ExtendRentOrBuyoutStripItemMessage : IMessageEvent
{
    public required int StripItemId { get; init; }

    public required bool Buyout { get; init; }
}
