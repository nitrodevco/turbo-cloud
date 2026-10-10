using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

namespace Turbo.Primitives.Gamedata;

/// <summary>
/// The ids that load a bundle (the effects sharing a library, a pet's type), as its row keeps
/// them: comma separated, ascending.
/// </summary>
public static class AssetBundleIds
{
    public const char SEPARATOR = ',';

    /// <summary>The ids a row keeps; what is not a number is let go.</summary>
    public static ImmutableArray<int> Parse(string? ids) =>
        string.IsNullOrEmpty(ids)
            ? []
            :
            [
                .. ids.Split(SEPARATOR, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x =>
                        int.TryParse(
                            x,
                            NumberStyles.AllowLeadingSign,
                            CultureInfo.InvariantCulture,
                            out var id
                        )
                            ? (int?)id
                            : null
                    )
                    .OfType<int>(),
            ];

    /// <summary>The ids as a row keeps them, distinct and ascending; null when there are none.</summary>
    public static string? Format(IEnumerable<int> ids)
    {
        var sorted = ids.Distinct().Order().ToList();

        return sorted.Count == 0
            ? null
            : string.Join(
                SEPARATOR,
                sorted.Select(x => x.ToString(CultureInfo.InvariantCulture))
            );
    }
}
