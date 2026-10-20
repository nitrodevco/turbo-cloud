using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

/// <summary>Flash <c>WiredUserHighlightMessageParser</c>: int count, the room indices, then the highlight.</summary>
internal class WiredUserHighlightMessageComposerSerializer(int header)
    : AbstractSerializer<WiredUserHighlightMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredUserHighlightMessageComposer message
    )
    {
        packet.WriteInteger(message.RoomIndices.Length);

        foreach (var index in message.RoomIndices)
            packet.WriteInteger(index);

        WiredVisualizationSerializer.SerializeHighlight(packet, message.Highlight);
    }
}
