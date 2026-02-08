using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Furniture;

internal class YoutubeDisplayPlaylistsMessageComposerSerializer(int header)
    : AbstractSerializer<YoutubeDisplayPlaylistsMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        YoutubeDisplayPlaylistsMessageComposer message
    )
    {
        //
    }
}
