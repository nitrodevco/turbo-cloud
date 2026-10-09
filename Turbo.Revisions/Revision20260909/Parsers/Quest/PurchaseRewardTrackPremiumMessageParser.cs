using Turbo.Primitives.Messages.Incoming.Quest;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Quest;

/// <summary>The premium confirmation's Unlock: the track id.</summary>
internal class PurchaseRewardTrackPremiumMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new PurchaseRewardTrackPremiumMessage { TrackId = packet.PopString() };
}
