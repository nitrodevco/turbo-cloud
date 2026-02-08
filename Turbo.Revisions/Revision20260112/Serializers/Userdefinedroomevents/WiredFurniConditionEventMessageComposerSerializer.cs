using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260112.Serializers.Userdefinedroomevents.Data;

namespace Turbo.Revisions.Revision20260112.Serializers.Userdefinedroomevents;

internal class WiredFurniConditionEventMessageComposerSerializer(int header)
    : AbstractSerializer<WiredFurniConditionEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredFurniConditionEventMessageComposer message
    )
    {
        WiredDataSerializer.Serialize(packet, message.WiredData);
    }
}
