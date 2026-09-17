using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Turbo.Primitives.Rooms;

/// <summary>Room tags are stored as one comma-separated column.</summary>
public static class RoomTags
{
    private const char SEPARATOR = ',';

    public static ImmutableArray<string> Parse(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            :
            [
                .. value
                    .Split(
                        SEPARATOR,
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                    )
                    .Distinct(StringComparer.OrdinalIgnoreCase),
            ];

    public static string Join(IEnumerable<string> tags) => string.Join(SEPARATOR, tags);

    /// <summary>
    /// Trims, lower-cases and de-duplicates tags, dropping empty ones and any containing the
    /// separator, then keeps at most <paramref name="maxTags"/> of at most
    /// <paramref name="maxLength"/> characters.
    /// </summary>
    public static ImmutableArray<string> Normalize(
        IEnumerable<string?> tags,
        int maxTags,
        int maxLength
    ) =>
        [
            .. tags.Select(x => x?.Trim().ToLowerInvariant() ?? string.Empty)
                .Where(x => x.Length > 0 && x.Length <= maxLength && !x.Contains(SEPARATOR))
                .Distinct(StringComparer.Ordinal)
                .Take(maxTags),
        ];
}
