using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Furniture.Providers;

namespace Turbo.Operations.Suggestions;

/// <summary>Furniture definition names, as <c>:giveitem</c> takes them.</summary>
public sealed class FurniSuggestions(IFurnitureDefinitionProvider definitionProvider)
    : ISuggestionSource
{
    public string Name => SuggestionSources.FURNI;

    public Task<IReadOnlyList<string>> SuggestAsync(
        CommandSuggestionContext context,
        string prefix,
        int limit,
        CancellationToken ct
    ) => Task.FromResult(definitionProvider.FindNames(prefix, limit));
}
