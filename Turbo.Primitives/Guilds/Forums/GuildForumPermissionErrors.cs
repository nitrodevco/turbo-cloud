namespace Turbo.Primitives.Guilds.Forums;

/// <summary>
/// Why a player may not do something in a forum, as the client turns it into
/// groupforum.view.error.&lt;error&gt;; empty means allowed.
/// </summary>
public static class GuildForumPermissionErrors
{
    public const string NONE = "";
    public const string NOT_MEMBER = "not_member";
    public const string NOT_ADMIN = "not_admin";
    public const string NOT_OWNER = "not_owner";
}
