using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Providers;

namespace Turbo.Operations.Suggestions;

/// <summary>The registered permission nodes, as <c>:perm check</c> takes them.</summary>
public sealed class NodeSuggestions(IPermissionRegistryProvider registryProvider)
    : ISuggestionSource
{
    public string Name => SuggestionSources.NODES;

    public Task<IReadOnlyList<string>> SuggestAsync(
        CommandSuggestionContext context,
        string prefix,
        int limit,
        CancellationToken ct
    ) =>
        Task.FromResult<IReadOnlyList<string>>([
            .. registryProvider
                .Current.Nodes.Keys.Where(x =>
                    x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                )
                .Order(StringComparer.Ordinal)
                .Take(limit),
        ]);
}
