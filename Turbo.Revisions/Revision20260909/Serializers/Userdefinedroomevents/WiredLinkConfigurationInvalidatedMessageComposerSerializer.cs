using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

internal class WiredLinkConfigurationInvalidatedMessageComposerSerializer(int header)
    : AbstractSerializer<WiredLinkConfigurationInvalidatedMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredLinkConfigurationInvalidatedMessageComposer message
    ) =>
        packet
            .WriteInteger(message.RoomId)
            .WriteInteger(message.FurniId)
            .WriteString(message.DeletedNetworkId);
}
