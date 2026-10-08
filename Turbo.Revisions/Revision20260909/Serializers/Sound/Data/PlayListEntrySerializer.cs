using System;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Sound.Snapshots;

namespace Turbo.Revisions.Revision20260909.Serializers.Sound.Data;

/// <summary>
/// A song as a sound machine's playlist entry (<c>PlayListEntry</c>): id, length in
/// milliseconds, name, author. <c>PlayList</c> and <c>PlayListSongAdded</c> both carry it.
/// </summary>
internal static class PlayListEntrySerializer
{
    public static void Serialize(IServerPacket packet, SongSnapshot song) =>
        packet
            .WriteInteger(song.Id)
            .WriteInteger(LengthMs(song))
            .WriteString(song.Name)
            .WriteString(song.Author);

    /// <summary>The client keeps song lengths in milliseconds.</summary>
    public static int LengthMs(SongSnapshot song) =>
        (int)TimeSpan.FromSeconds(song.LengthSeconds).TotalMilliseconds;
}
