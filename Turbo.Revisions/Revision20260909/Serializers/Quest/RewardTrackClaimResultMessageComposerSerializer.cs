using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Quest;

/// <summary>Track id, prize id, result code.</summary>
internal class RewardTrackClaimResultMessageComposerSerializer(int header)
    : AbstractSerializer<RewardTrackClaimResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        RewardTrackClaimResultMessageComposer message
    )
    {
        packet.WriteString(message.TrackId);
        packet.WriteString(message.PrizeId);
        packet.WriteInteger((int)message.Result);
    }
}
