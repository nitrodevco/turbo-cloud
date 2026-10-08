using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Turbo.Primitives.Texts;

namespace Turbo.Primitives.Moderation;

public static class WordFilterExtensions
{
    /// <summary>
    /// Client text filtered and made fit to store (<see cref="ClientText.Truncate"/>). The cut
    /// comes after the filter: a replacement longer than the word it replaces must not take the
    /// text past its column.
    /// </summary>
    public static string FilterAndTruncate(this IWordFilter filter, string? text, int maxLength) =>
        ClientText.Truncate(filter.Filter(text ?? string.Empty), maxLength);

    /// <summary>Each tag filtered, dropping any the filter leaves twice.</summary>
    public static ImmutableArray<string> FilterTags(
        this IWordFilter filter,
        IEnumerable<string> tags
    ) => [.. tags.Select(filter.Filter).Distinct()];
}
