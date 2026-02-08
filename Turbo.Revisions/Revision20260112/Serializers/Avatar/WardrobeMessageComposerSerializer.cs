using Turbo.Primitives.Messages.Outgoing.Avatar;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Avatar;

internal class WardrobeMessageComposerSerializer(int header)
    : AbstractSerializer<WardrobeMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, WardrobeMessageComposer message)
    {
        //
    }
}
