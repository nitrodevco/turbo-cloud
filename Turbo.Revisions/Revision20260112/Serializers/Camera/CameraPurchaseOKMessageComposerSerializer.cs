using Turbo.Primitives.Messages.Outgoing.Camera;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Camera;

internal class CameraPurchaseOKMessageComposerSerializer(int header)
    : AbstractSerializer<CameraPurchaseOKMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, CameraPurchaseOKMessageComposer message)
    {
        //
    }
}
