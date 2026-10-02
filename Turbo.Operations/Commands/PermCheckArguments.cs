using Turbo.Primitives.Commands;

namespace Turbo.Operations.Commands;

public sealed record PermCheckArguments(
    PlayerTarget Who,
    [Suggest(SuggestionSources.NODES)] string Node
) : PermArguments(Who, Node);
