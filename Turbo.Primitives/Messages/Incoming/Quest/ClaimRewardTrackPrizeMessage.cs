using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Quest;

public record ClaimRewardTrackPrizeMessage : IMessageEvent
{
    public required string TrackId { get; init; }

    public required string PrizeId { get; init; }
}
