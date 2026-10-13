using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

/// <summary>
/// Flash <c>AchievementResolutionProgressMessageParser</c> (the leaderboard chunk, misnamed) and
/// <c>variableleaderboard/_-02R.fromMessage</c>: int overlay, int query revision, string variable,
/// int chunk index, int total (never negative), int k (0..50); per row int rank, int user id,
/// string name, string figure, string value, long created, long updated, bool has level,
/// [string level], bool has text, [string text].
/// </summary>
internal class VariableLeaderboardChunkUpdateMessageComposerSerializer(int header)
    : AbstractSerializer<VariableLeaderboardChunkUpdateMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        VariableLeaderboardChunkUpdateMessageComposer message
    )
    {
        packet
            .WriteInteger(message.OverlayInstanceId)
            .WriteInteger(message.QueryRevision)
            .WriteString(message.SelectedVariableId)
            .WriteInteger(message.ChunkIndex)
            .WriteInteger(message.TotalCount)
            .WriteInteger(message.Rows.Length);

        foreach (var row in message.Rows)
        {
            packet
                .WriteInteger(row.Rank)
                .WriteInteger(row.UserId)
                .WriteString(row.Username)
                .WriteString(row.Figure)
                .WriteString(row.RawValue)
                .WriteLong(row.CreationTime)
                .WriteLong(row.LastUpdateTime)
                .WriteBoolean(row.Level is not null);

            if (row.Level is not null)
                packet.WriteString(row.Level);

            packet.WriteBoolean(row.TextConnection is not null);

            if (row.TextConnection is not null)
                packet.WriteString(row.TextConnection);
        }
    }
}
