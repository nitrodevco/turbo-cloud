namespace Turbo.Primitives.Guilds.Enums;

/// <summary>
/// What the wearer of a group badge is to that group, as a room's <c>Users</c> and
/// <c>FavoriteMembershipUpdate</c> messages carry it. Habbo sends a plain member as 3 and an admin
/// as 2 (official client capture 2026-10-13: four members of four groups at 3, an admin of group
/// 368818 at 2); the client only stores the number (RoomUsersHandler.as:158). The owner is not
/// recorded: 1 is inferred from the other two being the member rank plus one.
/// </summary>
public enum WornGuildStatus
{
    /// <summary>Wears no group badge.</summary>
    None = -1,

    Owner = 1,
    Admin = 2,
    Member = 3,
}

public static class WornGuildStatuses
{
    /// <summary>The worn status of a member of the given rank; a non-member wears nothing.</summary>
    public static WornGuildStatus FromRank(GuildMemberRank? rank) =>
        rank switch
        {
            GuildMemberRank.Owner => WornGuildStatus.Owner,
            GuildMemberRank.Admin => WornGuildStatus.Admin,
            GuildMemberRank.Member => WornGuildStatus.Member,
            _ => WornGuildStatus.None,
        };
}
