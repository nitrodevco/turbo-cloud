using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Session;

internal class GamePlayerValueMessageComposerSerializer(int header)
    : AbstractSerializer<GamePlayerValueMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, GamePlayerValueMessageComposer message)
    {
        packet.WriteInteger(message.UserId).WriteInteger(message.Value);
    }
}
