using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Engine;

internal class UserRemoveMessageComposerSerializer(int header)
    : AbstractSerializer<UserRemoveMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, UserRemoveMessageComposer message)
    {
        packet.WriteString(message.ObjectId.ToString());
    }
}
