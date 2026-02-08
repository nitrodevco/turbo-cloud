using Turbo.Primitives.Messages.Outgoing.Room.Action;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Action;

internal class ExpressionMessageComposerSerializer(int header)
    : AbstractSerializer<ExpressionMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, ExpressionMessageComposer message)
    {
        packet.WriteInteger(message.ObjectId).WriteInteger((int)message.ExpressionType);
    }
}
