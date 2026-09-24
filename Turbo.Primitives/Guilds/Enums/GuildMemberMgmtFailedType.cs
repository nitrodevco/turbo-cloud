namespace Turbo.Primitives.Guilds.Enums;

/// <summary>
/// Why acting on a member failed, looked up as <c>group.membermgmt.fail.&lt;n&gt;</c>. All three
/// are one admin finding that another got there first — the group grain serialises the acts
/// themselves, but two admins can still be looking at rosters a moment apart.
/// </summary>
public enum GuildMemberMgmtFailedType
{
    NoLongerMember = 0,
    AlreadyRejected = 1,
    AlreadyAccepted = 2,
}
