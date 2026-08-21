using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Catalog;

internal class GiftWrappingConfigurationEventMessageComposerSerializer(int header)
    : AbstractSerializer<GiftWrappingConfigurationEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        GiftWrappingConfigurationEventMessageComposer message
    )
    {
        //
    }
}
