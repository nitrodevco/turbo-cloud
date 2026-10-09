namespace Turbo.Primitives.Guilds.Forums.Enums;

/// <summary>
/// Who may do something in a group forum: the options of the forum settings' selectors, in
/// their order (groupforum.permissions.option_all, _group_members, _group_admins, _owner).
/// Moderation starts at admins (AS3 ForumSettingsView: the moderate selector's lowest is 2).
/// </summary>
public enum GuildForumPermission
{
    Everybody = 0,
    Members = 1,
    Admins = 2,
    Owner = 3,
}
