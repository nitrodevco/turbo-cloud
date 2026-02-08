using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Engine;

internal class ObjectRemoveMessageComposerSerializer(int header)
    : AbstractSerializer<ObjectRemoveMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, ObjectRemoveMessageComposer message)
    {
        packet
            .WriteString(message.ObjectId.ToString())
            .WriteBoolean(message.IsExpired)
            .WriteInteger(message.PickerId)
            .WriteInteger(message.Delay);
    }
}
