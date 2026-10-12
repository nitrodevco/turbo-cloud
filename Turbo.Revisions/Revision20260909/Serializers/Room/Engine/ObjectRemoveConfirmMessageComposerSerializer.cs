using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Engine;

internal class ObjectRemoveConfirmMessageComposerSerializer(int header)
    : AbstractSerializer<ObjectRemoveConfirmMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ObjectRemoveConfirmMessageComposer message
    )
    {
        packet
            .WriteInteger((int)message.Category)
            .WriteInteger(message.ObjectId)
            .WriteString(message.Confirmation.TitleKey)
            .WriteString(message.Confirmation.BodyKey);
    }
}
