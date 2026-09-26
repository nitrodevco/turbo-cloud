using Turbo.Primitives.Guilds.Enums;

namespace Turbo.Primitives.Guilds;

/// <summary>
/// Which ranks mean what. There is one definition of "is in the group" and one of "may act on
/// other members", because there were nine, in four spellings.
///
/// Two of those nine said it by exclusion — <c>Rank != Requested &amp;&amp; Rank != Blocked</c> —
/// and the rest by inclusion. Both read correctly today and would disagree the moment a rank is
/// added: the exclusions would quietly count it as a membership and the inclusions would not.
/// That is the whole reason this file exists, so add a rank here and nowhere else.
/// </summary>
public static class GuildMemberRanks
{
    /// <summary>Whether this rank is a membership at all. A request and a block are not.</summary>
    public static bool IsMember(GuildMemberRank? rank) =>
        rank is GuildMemberRank.Owner or GuildMemberRank.Admin or GuildMemberRank.Member;

    /// <summary>Whether this rank may approve, reject, kick, block and unblock.</summary>
    public static bool CanManage(GuildMemberRank? rank) =>
        rank is GuildMemberRank.Owner or GuildMemberRank.Admin;

    /// <summary>
    /// The membership ranks, for a query. A fresh array rather than a shared one so nothing can
    /// edit it, and because a plain array is what EF translates into an <c>IN</c>.
    /// </summary>
    public static GuildMemberRank[] MemberRanks() =>
        [GuildMemberRank.Owner, GuildMemberRank.Admin, GuildMemberRank.Member];

    /// <summary>The ranks that may act on other members, for a query.</summary>
    public static GuildMemberRank[] ManagingRanks() =>
        [GuildMemberRank.Owner, GuildMemberRank.Admin];
}
