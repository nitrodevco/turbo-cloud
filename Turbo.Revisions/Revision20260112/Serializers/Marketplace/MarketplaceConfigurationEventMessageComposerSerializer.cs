using Turbo.Primitives.Messages.Outgoing.Marketplace;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Marketplace;

internal class MarketplaceConfigurationEventMessageComposerSerializer(int header)
    : AbstractSerializer<MarketplaceConfigurationEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        MarketplaceConfigurationEventMessageComposer message
    )
    {
        //
    }
}
