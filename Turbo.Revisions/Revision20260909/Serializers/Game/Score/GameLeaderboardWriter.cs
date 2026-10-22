using Turbo.Primitives.Games.Snapshots;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Game.Score;

/// <summary>
/// The client's <c>Game2LeaderboardParser</c> (entries, list size, game type) and the week its
/// weekly forms read first (<c>Game2WeeklyLeaderboardParser</c>).
/// </summary>
internal static class GameLeaderboardWriter
{
    public static void WriteWeek(IServerPacket packet, GameLeaderboardSnapshot board) =>
        packet
            .WriteInteger(board.Year)
            .WriteInteger(board.Week)
            .WriteInteger(board.MaxOffset)
            .WriteInteger(board.CurrentOffset)
            .WriteInteger(board.MinutesUntilReset);

    public static void Write(IServerPacket packet, GameLeaderboardSnapshot board)
    {
        packet.WriteInteger(board.Entries.Length);

        foreach (var entry in board.Entries)
            packet
                .WriteInteger(entry.Id)
                .WriteInteger(entry.Score)
                .WriteInteger(entry.Rank)
                .WriteString(entry.Name)
                .WriteString(entry.Figure)
                .WriteString(entry.Gender);

        packet.WriteInteger(board.TotalListSize).WriteInteger(board.GameTypeId);
    }
}
