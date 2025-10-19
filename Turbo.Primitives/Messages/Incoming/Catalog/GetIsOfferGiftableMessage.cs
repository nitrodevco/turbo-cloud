using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Catalog;

public record GetIsOfferGiftableMessage : IMessageEvent
{
    public int OfferId { get; init; }
}
