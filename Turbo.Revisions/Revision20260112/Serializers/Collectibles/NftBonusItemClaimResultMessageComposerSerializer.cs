using Turbo.Primitives.Messages.Outgoing.Collectibles;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Collectibles;

internal class NftBonusItemClaimResultMessageComposerSerializer(int header)
    : AbstractSerializer<NftBonusItemClaimResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        NftBonusItemClaimResultMessageComposer message
    )
    {
        //
    }
}
