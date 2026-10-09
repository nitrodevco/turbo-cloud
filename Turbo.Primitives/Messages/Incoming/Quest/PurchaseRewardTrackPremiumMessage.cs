using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Quest;

public record PurchaseRewardTrackPremiumMessage : IMessageEvent
{
    public required string TrackId { get; init; }
}
