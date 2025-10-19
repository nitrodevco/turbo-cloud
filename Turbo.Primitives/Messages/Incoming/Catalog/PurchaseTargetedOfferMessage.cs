using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Catalog;

public record PurchaseTargetedOfferMessage : IMessageEvent
{
    public int OfferId { get; init; }
    public int Quantity { get; init; }
}
