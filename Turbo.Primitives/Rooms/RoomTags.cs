using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Turbo.Primitives.Rooms;

/// <summary>
/// A room's tags as the hotel keeps them: lower case, distinct, and without commas, which the
/// client's tag list uses between tags.
/// </summary>
public static class RoomTags
{
    private const char SEPARATOR = ',';

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
