using Turbo.Primitives.Commands;

namespace Turbo.Operations.Commands;

public sealed record GroupAddArguments(
    PlayerTarget Who,
    [Suggest(SuggestionSources.GROUPS)] string Group,
    [CommandParameter(Description = "How long membership lasts; omit for permanent membership")]
        CommandDuration? Duration = null
) : GroupArguments(Who, Group);
