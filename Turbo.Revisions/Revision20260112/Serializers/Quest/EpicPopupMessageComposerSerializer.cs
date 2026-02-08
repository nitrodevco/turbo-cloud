using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Quest;

internal class EpicPopupMessageComposerSerializer(int header)
    : AbstractSerializer<EpicPopupMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, EpicPopupMessageComposer message)
    {
        //
    }
}
