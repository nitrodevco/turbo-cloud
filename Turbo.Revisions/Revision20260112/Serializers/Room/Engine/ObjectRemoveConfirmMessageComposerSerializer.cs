using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Engine;

internal class ObjectRemoveConfirmMessageComposerSerializer(int header)
    : AbstractSerializer<ObjectRemoveConfirmMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ObjectRemoveConfirmMessageComposer message
    )
    {
        //
    }
}
