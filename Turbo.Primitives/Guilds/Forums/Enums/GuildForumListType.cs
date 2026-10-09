namespace Turbo.Primitives.Guilds.Forums.Enums;

/// <summary>The forum lists (groupforum.view.forums_list.&lt;code&gt;, groupforum/list/active|popular|my).</summary>
public enum GuildForumListType
{
    /// <summary>Public forums by posts in the last 7 days.</summary>
    MostActive = 0,

    /// <summary>Public forums by unique readers in the last 7 days.</summary>
    MostViewed = 1,

    /// <summary>The forums of the player's groups.</summary>
    MyForums = 2,
}
