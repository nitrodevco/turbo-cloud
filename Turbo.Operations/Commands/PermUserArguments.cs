using Turbo.Primitives.Commands;

namespace Turbo.Operations.Commands;

public sealed record PermUserArguments(
    PlayerTarget Who,
    PermUserAction Action,
    [Suggest(SuggestionSources.NODES)] string Node,
    [CommandParameter(Description = "Grant (true) or deny (false); omit to grant")]
        bool Value = true,
    [CommandParameter(Description = "How long it lasts; omit for good")]
        CommandDuration? Duration = null
) : PermArguments(Who, Node);

/// <summary>
/// What <c>:perm user</c> does to the node: <see cref="Set"/> it, take the permanent assignment
/// away (<see cref="Unset"/>) or the temporary one (<see cref="Unsettemp"/>), as the console's
/// <c>perm user</c> does.
/// </summary>
public enum PermUserAction
{
    Set,
    Unset,
    Unsettemp,
}
