using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Catalog;

internal class SellablePetPalettesMessageComposerSerializer(int header)
    : AbstractSerializer<SellablePetPalettesMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        SellablePetPalettesMessageComposer message
    )
    {
        //
    }
}
