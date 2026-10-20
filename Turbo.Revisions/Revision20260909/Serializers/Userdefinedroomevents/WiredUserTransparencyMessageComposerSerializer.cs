using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

internal class WiredUserTransparencyMessageComposerSerializer(int header)
    : AbstractSerializer<WiredUserTransparencyMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredUserTransparencyMessageComposer message
    )
    {
        packet.WriteInteger(message.RoomIndices.Length);

        foreach (var id in message.RoomIndices)
            packet.WriteInteger(id);

        packet
            .WriteInteger(message.Transparency)
            .WriteBoolean(message.AllowClickThrough)
            .WriteInteger(message.EasingMode);
    }
}
