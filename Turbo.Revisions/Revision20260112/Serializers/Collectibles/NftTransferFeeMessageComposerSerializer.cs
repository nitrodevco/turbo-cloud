using Turbo.Primitives.Messages.Outgoing.Collectibles;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Collectibles;

internal class NftTransferFeeMessageComposerSerializer(int header)
    : AbstractSerializer<NftTransferFeeMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, NftTransferFeeMessageComposer message)
    {
        //
    }
}
