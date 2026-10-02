using Turbo.Primitives.Rooms.Events.Player;

namespace Turbo.Primitives.Commands.Events;

/// <summary>
/// Raised after a line is bound and before the command runs; a handler may <see cref="Cancel"/>
/// it. It carries the command's descriptor, so a plugin reads its own attributes on its commands
/// (a roleplay's <c>[UsableWhileDead]</c>) and vetoes from its own state. Core knows nothing about
/// being dead. A handler tells the player why through the room, as the command would.
/// </summary>
public sealed record CommandExecutingEvent : PlayerEvent
{
    public required CommandDescriptor Descriptor { get; init; }

    /// <summary>The bound arguments record, of the type the command declares.</summary>
    public required object Arguments { get; init; }

    public bool IsCancelled { get; private set; }

    public void Cancel() => IsCancelled = true;
}
