using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

/// <summary>Flash <c>WiredUserVisualizationStateMessageParser</c>: the transparencies, then the highlights.</summary>
internal class WiredUserVisualizationStateMessageComposerSerializer(int header)
    : AbstractSerializer<WiredUserVisualizationStateMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredUserVisualizationStateMessageComposer message
    ) =>
        WiredVisualizationSerializer.SerializeState(
            packet,
            message.Transparencies,
            message.Highlights
        );
}
