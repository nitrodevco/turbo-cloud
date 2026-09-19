using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Furniture;

internal class YoutubeControlVideoMessageComposerSerializer(int header)
    : AbstractSerializer<YoutubeControlVideoMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        YoutubeControlVideoMessageComposer message
    )
    {
        packet.WriteInteger(message.FurniId).WriteInteger((int)message.State);
    }
}
