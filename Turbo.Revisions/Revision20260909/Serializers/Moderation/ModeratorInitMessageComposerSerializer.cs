using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Moderation;

internal class ModeratorInitMessageComposerSerializer(int header)
    : AbstractSerializer<ModeratorInitMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, ModeratorInitMessageComposer message)
    {
        var init = message.Init;

        // The open issues, each an IssueInfo body (ModeratorInitData.as:37-72).
        packet.WriteInteger(init.Issues.Length);

        foreach (var issue in init.Issues)
            ModIssueWriter.Write(packet, issue);

        packet.WriteInteger(init.MessageTemplates.Length);

        foreach (var template in init.MessageTemplates)
            packet.WriteString(template);

        // A list ModeratorInitData reads and drops.
        packet.WriteInteger(0);
        packet
            .WriteBoolean(init.CfhPermission)
            .WriteBoolean(init.ChatlogsPermission)
            .WriteBoolean(init.AlertPermission)
            .WriteBoolean(init.KickPermission)
            .WriteBoolean(init.BanPermission)
            .WriteBoolean(init.RoomAlertPermission)
            .WriteBoolean(init.RoomKickPermission);
        packet.WriteInteger(init.RoomMessageTemplates.Length);

        foreach (var template in init.RoomMessageTemplates)
            packet.WriteString(template);
    }
}
