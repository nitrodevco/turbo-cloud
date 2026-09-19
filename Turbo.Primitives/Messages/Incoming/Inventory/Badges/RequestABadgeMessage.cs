using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Inventory.Badges;

public record RequestABadgeMessage : IMessageEvent
{
    public required string RequestCode { get; init; }
}
