using Turbo.Primitives.Messages.Outgoing.Vault;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Vault;

internal class UpgradeChestResultMessageComposerSerializer(int header)
    : AbstractSerializer<UpgradeChestResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        UpgradeChestResultMessageComposer message
    )
    {
        packet.WriteInteger(message.ChestId).WriteInteger((int)message.Result);
    }
}
