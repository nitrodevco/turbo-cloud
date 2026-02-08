using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Userdefinedroomevents;

internal class OpenEventMessageComposerSerializer(int header)
    : AbstractSerializer<OpenEventMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, OpenEventMessageComposer message)
    {
        packet.WriteInteger(message.ItemId);
    }
}
