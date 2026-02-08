using Turbo.Primitives.Messages.Outgoing.Vault;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Vault;

internal class IncomeRewardClaimResponseMessageComposerSerializer(int header)
    : AbstractSerializer<IncomeRewardClaimResponseMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        IncomeRewardClaimResponseMessageComposer message
    )
    {
        packet.WriteByte((byte)message.RewardCategory).WriteBoolean(message.Result);
    }
}
