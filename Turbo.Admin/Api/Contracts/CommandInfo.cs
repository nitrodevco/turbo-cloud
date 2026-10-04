using System.Collections.Generic;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A command the signed-in player may run, with the help the game would show them.</summary>
public sealed record CommandInfo(
    string Name,
    IReadOnlyList<string> Aliases,
    string Description,
    string Category,
    bool NeedsRoom,
    IReadOnlyList<string> Help
);
