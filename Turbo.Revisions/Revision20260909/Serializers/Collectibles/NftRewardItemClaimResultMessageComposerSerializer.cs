using Turbo.Primitives.Messages.Outgoing.Collectibles;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Collectibles;

internal class NftRewardItemClaimResultMessageComposerSerializer(int header)
    : AbstractSerializer<NftRewardItemClaimResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        NftRewardItemClaimResultMessageComposer message
    )
    {
        //
    }
}
