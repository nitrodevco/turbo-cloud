using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Inventory.Badges;

public record GetBadgeInfoMessage : IMessageEvent
{
    public required string BadgeCode { get; init; }
}
