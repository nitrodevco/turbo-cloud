using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Catalog;

public record GetProductOfferMessage : IMessageEvent
{
    public int OfferId { get; init; }
}
