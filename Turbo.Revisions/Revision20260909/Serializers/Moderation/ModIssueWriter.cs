using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Moderation;

/// <summary>
/// An issue as <c>IssueInfoMessageParser</c> reads it, also each of <c>ModeratorInit</c>'s issues:
/// id, state, source, topic, age (ms), priority, grouping, reporter id and name, reported id and
/// name, picker id and name, message, chat record id, then the pattern matches (none kept).
/// </summary>
internal static class ModIssueWriter
{
    public static void Write(IServerPacket packet, ModIssueSnapshot issue)
    {
        packet
            .WriteInteger(issue.IssueId)
            .WriteInteger(issue.State)
            .WriteInteger(issue.CategoryId)
            .WriteInteger(issue.ReportedCategoryId)
            .WriteInteger(issue.IssueAgeInMilliseconds)
            .WriteInteger(issue.Priority)
            .WriteInteger(issue.GroupingId)
            .WriteInteger(issue.ReporterUserId)
            .WriteString(issue.ReporterUserName)
            .WriteInteger(issue.ReportedUserId)
            .WriteString(issue.ReportedUserName)
            .WriteInteger(issue.PickerUserId)
            .WriteString(issue.PickerUserName)
            .WriteString(issue.Message)
            .WriteInteger(issue.ChatRecordId)
            .WriteInteger(0);
    }
}
