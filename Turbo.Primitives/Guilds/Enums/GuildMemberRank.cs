namespace Turbo.Primitives.Guilds.Enums;

/// <summary>
/// What one member is to their group, as the client's <c>MemberData</c> numbers it. The client
/// reads the value rather than a set of flags: <c>owner</c> is <c>== 0</c>, <c>admin</c> is
/// <c>== 1</c>, <c>member</c> is <c>!= 3</c> and <c>blocked</c> is <c>== 4</c> — so a blocked
/// player still answers <c>member</c> there, and only the rank itself distinguishes them.
///
/// <see cref="Requested"/> and <see cref="Blocked"/> are rows in the membership table like any
/// other; neither is a member. Keeping them as rows is what lets a request be rejected and a
/// block outlive the kick that caused it.
/// </summary>
public enum GuildMemberRank
{
    Owner = 0,
    Admin = 1,
    Member = 2,

    /// <summary>Asked to join and waiting. Not a member.</summary>
    Requested = 3,

    /// <summary>Kicked with blocking on. Cannot rejoin until unblocked.</summary>
    Blocked = 4,
}
