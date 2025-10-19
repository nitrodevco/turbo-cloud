using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Catalog;

public record PurchaseRoomAdMessageMessage : IMessageEvent
{
    public int PageId { get; init; }
    public int OfferId { get; init; }
    public int FlatId { get; init; }
    public string? Name { get; init; }
    public bool Extended { get; init; }
    public string? Description { get; init; }
    public int CategoryId { get; init; }
}
