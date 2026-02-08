using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Session;

internal class YouAreNotSpectatorMessageComposerSerializer(int header)
    : AbstractSerializer<YouAreNotSpectatorMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        YouAreNotSpectatorMessageComposer message
    )
    {
        packet.WriteInteger(message.RoomId);
    }
}
