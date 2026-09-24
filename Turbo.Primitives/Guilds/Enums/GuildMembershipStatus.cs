namespace Turbo.Primitives.Guilds.Enums;

/// <summary>
/// Where one player stands with one group, as the client's group details window reads it. It is
/// not the member's rank: an admin and a plain member are both <see cref="Member"/>. See
/// <see cref="GuildMemberRank"/> for the rank.
/// </summary>
public enum GuildMembershipStatus
{
    NotMember = 0,
    Member = 1,

    /// <summary>Asked to join an <see cref="GuildType.Exclusive"/> group and waiting on an admin.</summary>
    Pending = 2,
}
