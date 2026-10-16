using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Catalog;

internal class HasClaimedProductResponseMessageComposerSerializer(int header)
    : AbstractSerializer<HasClaimedProductResponseMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        HasClaimedProductResponseMessageComposer message
    )
    {
        packet.WriteString(message.ClaimId).WriteBoolean(message.HasClaimed);
    }
}
