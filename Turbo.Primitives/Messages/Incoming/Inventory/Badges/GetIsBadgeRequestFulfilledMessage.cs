using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Inventory.Badges;

public record GetIsBadgeRequestFulfilledMessage : IMessageEvent
{
    public required string RequestCode { get; init; }
}
