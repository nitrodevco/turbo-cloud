using Turbo.Primitives.Messages.Outgoing.Camera;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Camera;

internal class CameraStorageUrlMessageComposerSerializer(int header)
    : AbstractSerializer<CameraStorageUrlMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, CameraStorageUrlMessageComposer message)
    {
        //
    }
}
