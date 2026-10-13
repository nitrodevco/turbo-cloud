using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Marketplace;

/// <summary>Flash <c>GetMarketplaceItemStatsComposer</c>: the category (1 floor, 2 wall, 3 limited), the furni type and a poster's data.</summary>
public record GetMarketplaceItemStatsMessage : IMessageEvent
{
    public required int Category { get; init; }
    public required int FurniTypeId { get; init; }
    public string? ExtraData { get; init; }
}
