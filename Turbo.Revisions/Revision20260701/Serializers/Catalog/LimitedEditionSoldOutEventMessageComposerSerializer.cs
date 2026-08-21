using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Catalog;

internal class LimitedEditionSoldOutEventMessageComposerSerializer(int header)
    : AbstractSerializer<LimitedEditionSoldOutEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        LimitedEditionSoldOutEventMessageComposer message
    )
    {
        //
    }
}
