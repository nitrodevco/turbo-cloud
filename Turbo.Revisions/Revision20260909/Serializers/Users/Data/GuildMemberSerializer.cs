using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Users.Data;

/// <summary>
/// One roster row. The member list, a membership request and a rank change all carry the same
/// five fields, so they share this.
/// </summary>
internal class GuildMemberSerializer
{
    /// <summary>The client prints this as given and parses nothing out of it.</summary>
    private const string MEMBER_SINCE_FORMAT = "dd-MM-yyyy";

    public static void Serialize(IServerPacket packet, GuildMemberSnapshot member) =>
        packet
            .WriteInteger((int)member.Rank)
            .WriteInteger(member.PlayerId)
            .WriteString(member.PlayerName)
            .WriteString(member.Figure)
            .WriteString(
                member.MemberSince.ToString(
                    MEMBER_SINCE_FORMAT,
                    System.Globalization.CultureInfo.InvariantCulture
                )
            );
}
