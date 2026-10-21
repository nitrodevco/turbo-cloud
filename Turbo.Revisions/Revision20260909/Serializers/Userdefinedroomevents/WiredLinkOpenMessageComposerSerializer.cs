using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

internal class WiredLinkOpenMessageComposerSerializer(int header)
    : AbstractSerializer<WiredLinkOpenMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, WiredLinkOpenMessageComposer message) =>
        packet.WriteInteger(message.FurniId);
}
