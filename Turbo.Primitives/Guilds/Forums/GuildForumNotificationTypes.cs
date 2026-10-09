namespace Turbo.Primitives.Guilds.Forums;

/// <summary>The forum notification types the hotel texts have (notification.&lt;type&gt;.*).</summary>
public static class GuildForumNotificationTypes
{
    public const string DELIVERED = "forums.delivered";
    public const string ACCESS_DENIED = "forums.error.access_denied";
    public const string SETTINGS_UPDATED = "forums.forum_settings_updated";
    public const string THREAD_HIDDEN = "forums.thread.hidden";
    public const string THREAD_RESTORED = "forums.thread.restored";
    public const string THREAD_LOCKED = "forums.thread.locked";
    public const string THREAD_UNLOCKED = "forums.thread.unlocked";
    public const string THREAD_PINNED = "forums.thread.pinned";
    public const string THREAD_UNPINNED = "forums.thread.unpinned";
    public const string MESSAGE_HIDDEN = "forums.message.hidden";
    public const string MESSAGE_RESTORED = "forums.message.restored";
}
