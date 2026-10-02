using Turbo.Primitives.Commands;

namespace Turbo.Rooms.Commands;

public sealed record CommandsArguments(
    [CommandParameter(Description = "A command name for detailed help")] string? Command = null
);
