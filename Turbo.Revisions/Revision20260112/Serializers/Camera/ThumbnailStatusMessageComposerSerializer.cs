using Turbo.Primitives.Messages.Outgoing.Camera;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Camera;

internal class ThumbnailStatusMessageComposerSerializer(int header)
    : AbstractSerializer<ThumbnailStatusMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, ThumbnailStatusMessageComposer message)
    {
        //
    }
}
