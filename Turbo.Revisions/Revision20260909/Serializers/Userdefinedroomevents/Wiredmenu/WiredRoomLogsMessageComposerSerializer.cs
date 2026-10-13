using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents.Wiredmenu;

/// <summary>AS3 <c>WiredLogPage</c>: each filter goes behind its own flag, the numeric ones as bytes.</summary>
internal class WiredRoomLogsMessageComposerSerializer(int header)
    : AbstractSerializer<WiredRoomLogsMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, WiredRoomLogsMessageComposer message)
    {
        var page = message.Page;

        packet
            .WriteInteger(page.TotalEntries)
            .WriteInteger(page.Page)
            .WriteInteger(page.PageSize)
            .WriteInteger(page.Entries.Length);

        foreach (var entry in page.Entries)
            packet
                .WriteLong(entry.Id)
                .WriteByte((byte)entry.Level)
                .WriteByte((byte)entry.Source)
                .WriteString(entry.Message)
                .WriteLong(entry.TimestampMs)
                .WriteString(entry.TimestampText);

        packet.WriteBoolean(page.LogLevelFilter >= 0);

        if (page.LogLevelFilter >= 0)
            packet.WriteByte((byte)page.LogLevelFilter);

        packet.WriteBoolean(page.LogSourceFilter >= 0);

        if (page.LogSourceFilter >= 0)
            packet.WriteByte((byte)page.LogSourceFilter);

        packet.WriteBoolean(page.Query.Length > 0);

        if (page.Query.Length > 0)
            packet.WriteString(page.Query);
    }
}
