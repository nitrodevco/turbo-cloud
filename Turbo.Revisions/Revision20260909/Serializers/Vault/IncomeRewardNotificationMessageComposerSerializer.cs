using Turbo.Primitives.Messages.Outgoing.Vault;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Vault;

/// <summary>One byte, the category (Flash parser vault/_-IX.as:15).</summary>
internal class IncomeRewardNotificationMessageComposerSerializer(int header)
    : AbstractSerializer<IncomeRewardNotificationMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        IncomeRewardNotificationMessageComposer message
    )
    {
        packet.WriteByte((byte)message.RewardCategory);
    }
}
