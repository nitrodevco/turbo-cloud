using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Furniture;

internal class YoutubeDisplayPlaylistsMessageComposerSerializer(int header)
    : AbstractSerializer<YoutubeDisplayPlaylistsMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        YoutubeDisplayPlaylistsMessageComposer message
    )
    {
        packet.WriteInteger(message.FurniId).WriteInteger(message.Playlists.Length);

        foreach (var playlist in message.Playlists)
            packet
                .WriteString(playlist.PlaylistId)
                .WriteString(playlist.Title)
                .WriteString(playlist.Description);

        packet.WriteString(message.SelectedPlaylistId);
    }
}
