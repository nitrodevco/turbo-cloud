using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Catalog;

public record PurchaseBasicMembershipExtensionMessage : IMessageEvent
{
    public int OfferId { get; init; }
}
