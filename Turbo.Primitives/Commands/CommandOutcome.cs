namespace Turbo.Primitives.Commands;

/// <summary>How a command line ended. It is the <c>outcome</c> tag of command telemetry.</summary>
public enum CommandOutcome
{
    /// <summary>The command ran and returned.</summary>
    Completed,

    /// <summary>The executor holds none of the command's nodes.</summary>
    Refused,

    /// <summary>The executor lacks the room controller level the command declares.</summary>
    RoomLevel,

    /// <summary>The arguments did not fit: usage, an unknown player, a value that does not parse.</summary>
    BindFailed,

    /// <summary>A plugin cancelled <c>CommandExecutingEvent</c>.</summary>
    Vetoed,

    /// <summary>Over the flood limit, dropped without muting the player's chat.</summary>
    Flood,

    /// <summary>The command threw; the exception was logged and contained.</summary>
    Error,

    /// <summary>The room's call was cancelled while the command ran.</summary>
    Canceled,

    /// <summary>The line waits for the executor's <c>:confirm</c>; nothing was done yet.</summary>
    AwaitingConfirmation,

    /// <summary>The command returned a domain refusal or failure without throwing.</summary>
    Failed,

    /// <summary>Some operations succeeded, but the command did not complete all requested work.</summary>
    Partial,
}
