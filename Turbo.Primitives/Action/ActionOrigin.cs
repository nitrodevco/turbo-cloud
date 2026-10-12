namespace Turbo.Primitives.Action;

public enum ActionOrigin
{
    System = 0,
    Player = 1,
    Plugin = 2,
    Wired = 3,
    Bot = 4,

    /// <summary>
    /// A staff member through the admin panel, acting on a room as the player they are, as if
    /// they stood in it: the room's rules apply to them as to that player.
    /// </summary>
    Panel = 5,
}
