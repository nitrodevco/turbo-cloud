using System;
using System.Collections.Generic;
using System.Linq;

namespace Turbo.Rooms.Wired;

/// <summary>
/// A list of names a box was given, read one way everywhere. The editor's text area takes one
/// name per line and sends the lines joined by tabs (<c>UsersByName.readStringParamFromForm</c>).
/// The "users by name" selector and the "user by name" source read it with two different
/// splits and two different comparisons before, so one name list picked different avatars in
/// the two places.
/// </summary>
public static class WiredNameList
{
    private static readonly char[] SEPARATORS = ['\t', '\r', '\n'];

    /// <summary>How a name a box was given is compared with an avatar's or a bot's: ordinal, case ignored.</summary>
    public static StringComparer Comparer => StringComparer.OrdinalIgnoreCase;

    /// <summary>The names in a box's text, trimmed, blanks dropped, compared by <see cref="Comparer"/>.</summary>
    public static HashSet<string> Parse(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? new HashSet<string>(Comparer)
            : text.Split(
                    SEPARATORS,
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                )
                .ToHashSet(Comparer);
}
