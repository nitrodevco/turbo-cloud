using Turbo.Primitives.Messages.Outgoing.Vault;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Vault;

internal class CoinsChestContentsMessageComposerSerializer(int header)
    : AbstractSerializer<CoinsChestContentsMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CoinsChestContentsMessageComposer message
    )
    {
        packet
            .WriteInteger(message.ChestId)
            .WriteInteger(message.Coins)
            .WriteBoolean(message.IsUpdate);
    }
}
