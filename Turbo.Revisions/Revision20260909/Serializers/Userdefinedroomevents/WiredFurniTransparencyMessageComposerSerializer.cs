using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

internal class WiredFurniTransparencyMessageComposerSerializer(int header)
    : AbstractSerializer<WiredFurniTransparencyMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredFurniTransparencyMessageComposer message
    )
    {
        packet.WriteInteger(message.FurniIds.Length);

        foreach (var id in message.FurniIds)
            packet.WriteInteger(id);

        packet
            .WriteInteger(message.Transparency)
            .WriteBoolean(message.AllowClickThrough)
            .WriteInteger(message.EasingMode);
    }
}
