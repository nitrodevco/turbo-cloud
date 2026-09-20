using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

internal class WiredClickSettingsMessageComposerSerializer(int header)
    : AbstractSerializer<WiredClickSettingsMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredClickSettingsMessageComposer message
    )
    {
        packet.WriteInteger((int)message.UserOption).WriteInteger((int)message.FurniOption);
    }
}
