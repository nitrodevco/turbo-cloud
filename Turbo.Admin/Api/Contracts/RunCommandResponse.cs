using System.Collections.Generic;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// What running a line did. <see cref="Outcome"/> is the runner's outcome (<c>Completed</c>,
/// <c>Refused</c>, <c>BindFailed</c>, <c>AwaitingConfirmation</c>, ...), null when no command has
/// that name.
/// </summary>
public sealed record RunCommandResponse(
    bool Found,
    string? Outcome,
    IReadOnlyList<CommandOutputLine> Lines
);
