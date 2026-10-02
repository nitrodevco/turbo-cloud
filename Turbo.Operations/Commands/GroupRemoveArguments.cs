using Turbo.Primitives.Commands;

namespace Turbo.Operations.Commands;

public sealed record GroupRemoveArguments(
    PlayerTarget Who,
    [Suggest(SuggestionSources.GROUPS)] string Group
) : GroupArguments(Who, Group);
