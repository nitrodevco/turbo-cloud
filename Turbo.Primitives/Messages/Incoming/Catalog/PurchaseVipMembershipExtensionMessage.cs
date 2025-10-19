using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Catalog;

public record PurchaseVipMembershipExtensionMessage : IMessageEvent
{
    public int OfferId { get; init; }
}
