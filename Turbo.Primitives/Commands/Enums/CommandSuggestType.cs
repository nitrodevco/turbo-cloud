namespace Turbo.Primitives.Commands.Enums;

/// <summary>
/// Where a client finds the values to offer for a parameter. On the wire of <c>chat.commands</c>.
/// </summary>
public enum CommandSuggestType
{
    /// <summary>Nothing to offer: a number, a free word, the rest of the line.</summary>
    None = 0,

    /// <summary>
    /// The client knows them itself: the members the tree carries, the room's own users, the usual
    /// durations.
    /// </summary>
    Client = 1,

    /// <summary>Only the server knows them; the client asks with <c>TurboCommandSuggestMessage</c>.</summary>
    Server = 2,
}
