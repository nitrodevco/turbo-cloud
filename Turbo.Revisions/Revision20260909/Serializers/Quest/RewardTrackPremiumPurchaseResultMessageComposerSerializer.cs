using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Quest;

/// <summary>Track id, result code, the track's points.</summary>
internal class RewardTrackPremiumPurchaseResultMessageComposerSerializer(int header)
    : AbstractSerializer<RewardTrackPremiumPurchaseResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        RewardTrackPremiumPurchaseResultMessageComposer message
    )
    {
        packet.WriteString(message.TrackId);
        packet.WriteInteger((int)message.Result);
        packet.WriteInteger(message.Points);
    }
}
