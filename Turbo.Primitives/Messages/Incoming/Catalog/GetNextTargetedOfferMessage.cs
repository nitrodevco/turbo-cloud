using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Catalog;

public record GetNextTargetedOfferMessage : IMessageEvent
{
    public int OfferId { get; init; }
}
