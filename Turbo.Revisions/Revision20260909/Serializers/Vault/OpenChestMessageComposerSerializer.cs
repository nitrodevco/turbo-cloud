using Turbo.Primitives.Messages.Outgoing.Vault;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Vault;

internal class OpenChestMessageComposerSerializer(int header)
    : AbstractSerializer<OpenChestMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, OpenChestMessageComposer message)
    {
        packet.WriteInteger(message.ChestId);
    }
}
