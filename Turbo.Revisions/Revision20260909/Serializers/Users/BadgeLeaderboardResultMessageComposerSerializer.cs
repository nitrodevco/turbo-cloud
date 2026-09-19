using Turbo.Primitives.Badges.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Users;

internal class BadgeLeaderboardResultMessageComposerSerializer(int header)
    : AbstractSerializer<BadgeLeaderboardResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        BadgeLeaderboardResultMessageComposer message
    )
    {
        var page = message.Page;

        packet
            .WriteInteger((int)page.Type)
            .WriteInteger(page.Rarity)
            .WriteInteger(page.ChunkIndex)
            .WriteInteger(page.ChunkSize)
            .WriteInteger(page.TotalEntries)
            .WriteInteger(page.Entries.Length);

        foreach (var entry in page.Entries)
            WriteEntry(packet, entry);

        packet.WriteBoolean(page.OwnEntry is not null);

        if (page.OwnEntry is not null)
            WriteEntry(packet, page.OwnEntry);
    }

    private static void WriteEntry(IServerPacket packet, BadgeLeaderboardEntrySnapshot entry) =>
        packet
            .WriteInteger(entry.PlayerId)
            .WriteString(entry.Name)
            .WriteString(entry.Figure)
            .WriteInteger(entry.Rank)
            .WriteInteger(entry.Score);
}
