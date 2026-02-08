using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260112.Serializers.Userdefinedroomevents.Data;

namespace Turbo.Revisions.Revision20260112.Serializers.Userdefinedroomevents;

internal class WiredFurniSelectorEventMessageComposerSerializer(int header)
    : AbstractSerializer<WiredFurniSelectorEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredFurniSelectorEventMessageComposer message
    )
    {
        WiredDataSerializer.Serialize(packet, message.WiredData);
    }
}
