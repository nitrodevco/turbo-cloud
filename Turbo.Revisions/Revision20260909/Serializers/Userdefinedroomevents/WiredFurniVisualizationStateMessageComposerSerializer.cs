using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

/// <summary>Flash <c>WiredFurniVisualizationStateMessageParser</c>: the transparencies, then the highlights.</summary>
internal class WiredFurniVisualizationStateMessageComposerSerializer(int header)
    : AbstractSerializer<WiredFurniVisualizationStateMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredFurniVisualizationStateMessageComposer message
    ) =>
        WiredVisualizationSerializer.SerializeState(
            packet,
            message.Transparencies,
            message.Highlights
        );
}
