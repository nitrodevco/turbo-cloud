using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Turbo.Gamedata.Texts;

/// <summary>
/// Habbo's external texts file: one <c>key=value</c> per line. It is read as the client reads it
/// (<c>CoreLocalizationManager.parseLocalizationData</c>): lines split on any run of line breaks,
/// a line starting with <c>#</c> is a comment, the key ends at the first <c>=</c> (values carry
/// <c>=</c> of their own), both sides trimmed. A key given twice keeps its first place and its
/// last value. Values are kept as written - <c>\n</c> escapes and all - so a file written back
/// reads the same.
/// </summary>
internal static partial class ExternalTextsFile
{
    public static Dictionary<string, string> Parse(string text)
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in LineBreaks().Split(text))
        {
            if (line.StartsWith('#'))
                continue;

            var separator = line.IndexOf('=', StringComparison.Ordinal);

            if (separator <= 0)
                continue;

            var key = line[..separator].Trim();

            if (key.Length == 0)
                continue;

            texts[key] = line[(separator + 1)..].Trim();
        }

        return texts;
    }

    /// <summary>The texts as the file, by key in ordinal order, UTF-8.</summary>
    public static byte[] Write(IEnumerable<(string Key, string Value)> texts)
    {
        var builder = new StringBuilder();

        foreach (var (key, value) in texts.OrderBy(x => x.Key, StringComparer.Ordinal))
            builder.Append(key).Append('=').Append(value).Append('\n');

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    [GeneratedRegex(@"\n\r+|\n+|\r+")]
    private static partial Regex LineBreaks();
}
