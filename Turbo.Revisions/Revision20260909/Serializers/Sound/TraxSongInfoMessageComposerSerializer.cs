using Turbo.Primitives.Messages.Outgoing.Sound;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Sound.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Sound;

/// <summary>
/// Each song as <c>TraxSongInfoMessageParser</c> reads it: id, code, name, track, length in
/// milliseconds, author.
/// </summary>
internal class TraxSongInfoMessageComposerSerializer(int header)
    : AbstractSerializer<TraxSongInfoMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, TraxSongInfoMessageComposer message)
    {
        packet.WriteInteger(message.Songs.Length);

        foreach (var song in message.Songs)
            packet
                .WriteInteger(song.Id)
                .WriteString(song.Code)
                .WriteString(song.Name)
                .WriteString(song.Track)
                .WriteInteger(PlayListEntrySerializer.LengthMs(song))
                .WriteString(song.Author);
    }
}
