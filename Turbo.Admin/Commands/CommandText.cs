using System;
using System.Linq;

namespace Turbo.Admin.Commands;

/// <summary>Free text the panel puts into an operator command line: a reason, a message.</summary>
public static class CommandText
{
    /// <summary>The longest reason or message the panel passes on, in characters.</summary>
    public const int MAX_LENGTH = 500;

    /// <summary>
    /// The text as one line of plain words: line breaks and tabs become spaces, so it cannot start
    /// another command, and it is cut to <see cref="MAX_LENGTH"/>. Null when nothing is left.
    /// </summary>
    public static string? OneLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var flat = string.Join(
            ' ',
            text.Split((char[])['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
        );

        return flat.Length > MAX_LENGTH ? flat[..MAX_LENGTH] : flat;
    }
}
