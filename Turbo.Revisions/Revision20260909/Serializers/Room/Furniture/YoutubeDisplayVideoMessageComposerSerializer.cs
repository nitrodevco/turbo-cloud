using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Furniture;

internal class YoutubeDisplayVideoMessageComposerSerializer(int header)
    : AbstractSerializer<YoutubeDisplayVideoMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        YoutubeDisplayVideoMessageComposer message
    )
    {
        packet
            .WriteInteger(message.FurniId)
            .WriteString(message.VideoId)
            .WriteInteger(message.StartAtSeconds)
            .WriteInteger(message.EndAtSeconds)
            .WriteInteger((int)message.State);
    }
}
