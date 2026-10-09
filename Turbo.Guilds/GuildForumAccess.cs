using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Forums;
using Turbo.Primitives.Guilds.Forums.Enums;

namespace Turbo.Guilds;

/// <summary>
/// What one player may do in a forum: their rank in the group and whether they are staff (the
/// moderation tool). Staff may do everything, as the client's isStaff flag lets them restore
/// what staff hid.
/// </summary>
internal readonly record struct GuildForumAccess(GuildMemberRank? Rank, bool IsStaff)
{
    /// <summary>Why the player may not do what <paramref name="permission"/> guards; empty when they may.</summary>
    public string Error(GuildForumPermission permission) =>
        IsStaff
            ? GuildForumPermissionErrors.NONE
            : permission switch
            {
                GuildForumPermission.Everybody => GuildForumPermissionErrors.NONE,
                GuildForumPermission.Members when GuildMemberRanks.IsMember(Rank) =>
                    GuildForumPermissionErrors.NONE,
                GuildForumPermission.Members => GuildForumPermissionErrors.NOT_MEMBER,
                GuildForumPermission.Admins when GuildMemberRanks.CanManage(Rank) =>
                    GuildForumPermissionErrors.NONE,
                GuildForumPermission.Admins => GuildForumPermissionErrors.NOT_ADMIN,
                _ when Rank == GuildMemberRank.Owner => GuildForumPermissionErrors.NONE,
                _ => GuildForumPermissionErrors.NOT_OWNER,
            };

    public bool Can(GuildForumPermission permission) =>
        Error(permission) == GuildForumPermissionErrors.NONE;

    /// <summary>The settings are the owner's ("Only me" is the owner's option).</summary>
    public bool CanChangeSettings => IsStaff || Rank == GuildMemberRank.Owner;

    /// <summary>
    /// Whether the player may see a post in this state: anything hidden only by those who may
    /// moderate, and what staff hid only by staff (AS3 MessageListView / ThreadListView).
    /// </summary>
    public bool MaySee(GuildForumState state, GuildForumPermission moderate) =>
        state switch
        {
            GuildForumState.HiddenByStaff => IsStaff,
            GuildForumState.HiddenByAdmin => Can(moderate),
            _ => true,
        };

    /// <summary>
    /// Whether the player may move a post from <paramref name="current"/> to
    /// <paramref name="requested"/>: a forum moderator hides with 10 and restores, staff also
    /// hide with 20 and alone restore what staff hid (AS3 GroupForumController).
    /// </summary>
    public bool MayModerate(
        GuildForumState current,
        int requested,
        GuildForumPermission moderate
    ) =>
        (GuildForumState)requested switch
        {
            GuildForumState.HiddenByStaff => IsStaff,
            GuildForumState.HiddenByAdmin => Can(moderate),
            GuildForumState.Restored => current == GuildForumState.HiddenByStaff
                ? IsStaff
                : Can(moderate),
            _ => false,
        };
}
