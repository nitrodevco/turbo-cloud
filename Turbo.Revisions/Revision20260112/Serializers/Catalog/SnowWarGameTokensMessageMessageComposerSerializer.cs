using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Catalog;

internal class SnowWarGameTokensMessageMessageComposerSerializer(int header)
    : AbstractSerializer<SnowWarGameTokensMessageMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        SnowWarGameTokensMessageMessageComposer message
    )
    {
        //
    }
}
