using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Catalog;

public record GetProductOfferMessage : IMessageEvent
{
    public int OfferId { get; init; }
}
