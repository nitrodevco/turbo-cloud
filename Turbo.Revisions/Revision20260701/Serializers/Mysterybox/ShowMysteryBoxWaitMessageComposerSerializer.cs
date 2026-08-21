using Turbo.Primitives.Messages.Outgoing.Mysterybox;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Mysterybox;

internal class ShowMysteryBoxWaitMessageComposerSerializer(int header)
    : AbstractSerializer<ShowMysteryBoxWaitMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ShowMysteryBoxWaitMessageComposer message
    )
    {
        //
    }
}
