using Turbo.Primitives.Messages.Outgoing.Camera;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Camera;

internal class CameraPublishStatusMessageComposerSerializer(int header)
    : AbstractSerializer<CameraPublishStatusMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CameraPublishStatusMessageComposer message
    )
    {
        //
    }
}
