namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>
/// What clicking another avatar does while a wired click setting is on, as the client's
/// <c>WiredEnvironment</c> numbers it. The client applies it and drops it on leaving the room.
/// </summary>
public enum WiredClickUserType
{
    Default = 0,

    /// <summary>The click still reaches the avatar, and the player walks to the tile behind it.</summary>
    ClickAndWalkBehind = 1,

    /// <summary>Avatars cannot be clicked: the click lands on whatever is behind them.</summary>
    PassThrough = 2,
}
