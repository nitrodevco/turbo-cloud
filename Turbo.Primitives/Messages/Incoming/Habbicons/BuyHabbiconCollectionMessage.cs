using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Habbicons;

public record BuyHabbiconCollectionMessage : IMessageEvent
{
    public required int CollectionId { get; init; }
}
