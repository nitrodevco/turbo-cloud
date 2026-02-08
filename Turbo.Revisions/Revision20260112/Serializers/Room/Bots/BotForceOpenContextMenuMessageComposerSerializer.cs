using Turbo.Primitives.Messages.Outgoing.Room.Bots;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Bots;

internal class BotForceOpenContextMenuMessageComposerSerializer(int header)
    : AbstractSerializer<BotForceOpenContextMenuMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        BotForceOpenContextMenuMessageComposer message
    )
    {
        //
    }
}
