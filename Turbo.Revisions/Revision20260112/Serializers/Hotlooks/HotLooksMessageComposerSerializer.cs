using Turbo.Primitives.Messages.Outgoing.Hotlooks;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Hotlooks;

internal class HotLooksMessageComposerSerializer(int header)
    : AbstractSerializer<HotLooksMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, HotLooksMessageComposer message)
    {
        //
    }
}
