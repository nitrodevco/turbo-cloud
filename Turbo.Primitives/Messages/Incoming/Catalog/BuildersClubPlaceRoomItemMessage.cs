using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Catalog;

public record BuildersClubPlaceRoomItemMessage : IMessageEvent
{
    public int PageId { get; init; }
    public int OfferId { get; init; }
    public string? ExtraParam { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Direction { get; init; }
}
