using Turbo.Primitives.Messages.Outgoing.Callforhelp;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Callforhelp;

internal class MyCfhReportStatusMessageComposerSerializer(int header)
    : AbstractSerializer<MyCfhReportStatusMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        MyCfhReportStatusMessageComposer message
    )
    {
        packet.WriteInteger(message.Reports.Length);

        foreach (var report in message.Reports)
        {
            packet
                .WriteLong(report.Id)
                .WriteLong(report.CreatedAtMs)
                .WriteString(report.Message)
                .WriteInteger(report.TopicId)
                .WriteString(report.ReportedName)
                .WriteLong(report.ClosedAtMs)
                .WriteBoolean(report.Sanctioned)
                .WriteBoolean(report.SanctionedByAutoModeration)
                .WriteByte((byte)report.AppealStatus)
                .WriteLong(report.AppealCreatedAtMs)
                .WriteLong(report.AppealResolvedAtMs);
        }
    }
}
