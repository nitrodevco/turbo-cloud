using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

/// <summary>Flash <c>WiredFurniHighlightMessageParser</c>: int count, the furni ids, then the highlight.</summary>
internal class WiredFurniHighlightMessageComposerSerializer(int header)
    : AbstractSerializer<WiredFurniHighlightMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredFurniHighlightMessageComposer message
    )
    {
        packet.WriteInteger(message.FurniIds.Length);

        foreach (var id in message.FurniIds)
            packet.WriteInteger(id);

        WiredVisualizationSerializer.SerializeHighlight(packet, message.Highlight);
    }
}
