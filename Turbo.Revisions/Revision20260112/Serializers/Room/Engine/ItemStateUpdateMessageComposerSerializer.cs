using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Engine;

internal class ItemStateUpdateMessageComposerSerializer(int header)
    : AbstractSerializer<ItemStateUpdateMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, ItemStateUpdateMessageComposer message)
    {
        packet.WriteInteger((int)message.ObjectId).WriteString(message.State);
    }
}
