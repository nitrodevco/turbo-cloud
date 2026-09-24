using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Users.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Users;

internal class GuildMembersMessageComposerSerializer(int header)
    : AbstractSerializer<GuildMembersMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, GuildMembersMessageComposer message)
    {
        var page = message.Page;

        packet
            .WriteInteger(page.Guild.GuildId)
            .WriteString(page.Guild.Name)
            .WriteInteger(page.Guild.RoomId)
            .WriteString(page.Guild.BadgeCode)
            .WriteInteger(page.TotalEntries)
            .WriteInteger(page.Members.Length);

        foreach (var member in page.Members)
            GuildMemberSerializer.Serialize(packet, member);

        packet
            .WriteBoolean(page.AllowedToManage)
            .WriteInteger(page.PageSize)
            .WriteInteger(page.PageIndex)
            .WriteInteger((int)page.SearchType)
            .WriteString(page.NameFilter);
    }
}
