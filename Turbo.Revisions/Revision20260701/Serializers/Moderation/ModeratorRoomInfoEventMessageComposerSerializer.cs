using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Moderation;

internal class ModeratorRoomInfoEventMessageComposerSerializer(int header)
    : AbstractSerializer<ModeratorRoomInfoEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ModeratorRoomInfoEventMessageComposer message
    )
    {
        //
    }
}
