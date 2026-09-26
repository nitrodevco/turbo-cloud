using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Texts;

namespace Turbo.Revisions.Revision20260909.Serializers.Users.Data;

/// <summary>
/// One roster row. The member list, a membership request and a rank change all carry the same
/// five fields, so they share this.
/// </summary>
internal class GuildMemberSerializer
{
    public static void Serialize(IServerPacket packet, GuildMemberSnapshot member) =>
        packet
            .WriteInteger((int)member.Rank)
            .WriteInteger(member.PlayerId)
            .WriteString(member.PlayerName)
            .WriteString(member.Figure)
            .WriteString(ClientDates.Format(member.MemberSince));
}
