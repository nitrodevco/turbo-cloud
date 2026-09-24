namespace Turbo.Primitives.Guilds.Enums;

/// <summary>
/// Which slice of a group's roster the member window is asking for, as the index of its dropdown.
///
/// The client only puts <see cref="Pending"/> and <see cref="Blocked"/> in that dropdown for
/// somebody allowed to manage the group, and <see cref="Blocked"/> only when the hotel sets
/// <c>group.blocking.enabled</c>. That makes the dropdown a courtesy, not a gate: a client that
/// asks for either without the right to is answered as <see cref="All"/>.
/// </summary>
public enum GuildMemberSearchType
{
    /// <summary>Everyone who is a member, admins and owner included.</summary>
    All = 0,
    Admins = 1,
    Pending = 2,
    Blocked = 3,
}
