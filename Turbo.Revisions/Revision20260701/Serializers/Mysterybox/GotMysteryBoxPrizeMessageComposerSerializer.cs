using Turbo.Primitives.Messages.Outgoing.Mysterybox;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Mysterybox;

internal class GotMysteryBoxPrizeMessageComposerSerializer(int header)
    : AbstractSerializer<GotMysteryBoxPrizeMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        GotMysteryBoxPrizeMessageComposer message
    )
    {
        //
    }
}
