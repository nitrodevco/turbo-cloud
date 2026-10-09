using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

internal class WiredClickUserResponseMessageComposerSerializer(int header)
    : AbstractSerializer<WiredClickUserResponseMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredClickUserResponseMessageComposer message
    )
    {
        packet.WriteInteger(message.ObjectId).WriteBoolean(message.OpenMenu);
    }
}
