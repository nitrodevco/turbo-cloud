namespace Turbo.Primitives.Commands.Enums;

/// <summary>
/// The type of one parameter of a command's arguments record. The values are on the wire of
/// <c>chat.commands</c> (<c>docs/client-capabilities.md</c>): a client that meets one it does not
/// know treats it as a word, so a value may be added but never renumbered.
/// </summary>
public enum CommandParameterKind
{
    /// <summary>One word, <c>string</c>.</summary>
    Word = 0,

    /// <summary><c>int</c>.</summary>
    Integer = 1,

    /// <summary><c>long</c>.</summary>
    Long = 2,

    /// <summary><c>bool</c>: true, false, on, off, yes, no, 1 or 0.</summary>
    Boolean = 3,

    /// <summary>A member of an enum, by name, ignoring case.</summary>
    Enumeration = 4,

    /// <summary>A player in the room the line is typed in (<c>IRoomPlayer</c>).</summary>
    RoomPlayer = 5,

    /// <summary>A player anywhere, by name (<see cref="PlayerTarget"/>).</summary>
    Player = 6,

    /// <summary>A <see cref="CommandDuration"/>.</summary>
    Duration = 7,

    /// <summary>Everything left on the line (<see cref="RestOfLine"/>).</summary>
    Rest = 8,
}
