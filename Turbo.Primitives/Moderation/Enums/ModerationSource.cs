namespace Turbo.Primitives.Moderation.Enums;

/// <summary>Where a moderation action was taken, as <c>moderation_actions</c> records it.</summary>
public enum ModerationSource : byte
{
    /// <summary>In the game: the client's own menus, or a command typed in chat.</summary>
    Game = 0,

    /// <summary>The admin panel.</summary>
    Panel = 1,

    /// <summary>The server console.</summary>
    Console = 2,

    /// <summary>
    /// The hotel itself: a room's wired, its flood protection. There is no actor.
    /// </summary>
    System = 3,
}
