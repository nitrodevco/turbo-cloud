using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Turbo.Primitives.Rooms.Wired;

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

    /// <summary>
    /// What Habbo refuses a "users by name" list with, as the error text and its parameter; null
    /// for a list it saves. Users By Name saved a 30-character name and 20 names, and refused 31
    /// characters with <c>wiredfurni.error.username_length</c> {max_username_length: "30"} and 21
    /// names with <c>usernames_count</c> {max_usernames: "20"} (official client capture
    /// 2026-10-14, NOTES-iv-save-errors).
    /// </summary>
    public static (string Key, (string, string) Parameter)? Refusal(
        string? text,
        int maxNameLength,
        int maxNames
    )
    {
        var names = string.IsNullOrWhiteSpace(text)
            ? []
            : text.Split(
                SEPARATORS,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            );

        if (names.Any(x => x.Length > maxNameLength))
            return (
                WiredSaveErrors.USERNAME_LENGTH,
                ("max_username_length", maxNameLength.ToString(CultureInfo.InvariantCulture))
            );

        if (names.Length > maxNames)
            return (
                WiredSaveErrors.USERNAMES_COUNT,
                ("max_usernames", maxNames.ToString(CultureInfo.InvariantCulture))
            );

        return null;
    }

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
