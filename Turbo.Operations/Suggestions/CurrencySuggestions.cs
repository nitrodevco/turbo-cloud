using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Providers;

namespace Turbo.Operations.Suggestions;

/// <summary>The enabled currency types, by the names <c>:give</c> takes.</summary>
public sealed class CurrencySuggestions(ICurrencyTypeProvider currencyTypeProvider)
    : ISuggestionSource
{
    public string Name => SuggestionSources.CURRENCIES;

    public Task<IReadOnlyList<string>> SuggestAsync(
        CommandSuggestionContext context,
        string prefix,
        int limit,
        CancellationToken ct
    ) =>
        Task.FromResult<IReadOnlyList<string>>([
            .. currencyTypeProvider
                .GetEnabledCurrencyNames()
                .Where(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.OrdinalIgnoreCase)
                .Take(limit),
        ]);
}
