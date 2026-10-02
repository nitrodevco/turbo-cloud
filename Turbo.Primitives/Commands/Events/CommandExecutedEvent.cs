using Turbo.Primitives.Rooms.Events.Player;

namespace Turbo.Primitives.Commands.Events;

/// <summary>Raised once a command line has ended, whatever the outcome.</summary>
public sealed record CommandExecutedEvent : PlayerEvent
{
    public required CommandDescriptor Descriptor { get; init; }

    public required CommandOutcome Outcome { get; init; }

    /// <summary>What the command returned; its default when it did not run to the end.</summary>
    public CommandResult Result { get; init; }
}
