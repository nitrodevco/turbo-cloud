using Turbo.Primitives.Messages.Incoming.Quest;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Quest;

/// <summary>A prize's claim: the track id, then the prize id (AS3 ClaimRewardTrackPrizeMessageComposer).</summary>
internal class ClaimRewardTrackPrizeMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new ClaimRewardTrackPrizeMessage
        {
            TrackId = packet.PopString(),
            PrizeId = packet.PopString(),
        };
}
