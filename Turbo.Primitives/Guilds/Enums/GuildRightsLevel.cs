namespace Turbo.Primitives.Guilds.Enums;

/// <summary>
/// How far down the group rights in its homeroom reach, as the editor's decoration setting
/// numbers it (<c>group.edit.settings.decoration.*</c>).
///
/// The group details packet reduces this to one <c>membersCanDecorate</c> boolean, which only
/// draws an icon. Nothing is enforced client-side: <c>RoomSecurityModule</c> is where this
/// becomes a controller level.
/// </summary>
public enum GuildRightsLevel
{
    /// <summary>Only the group owner, who is the room owner anyway.</summary>
    Owner = 0,

    /// <summary>The owner and the group's admins.</summary>
    Admins = 1,

    /// <summary>Every member.</summary>
    Members = 2,
}
