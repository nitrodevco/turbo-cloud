using System.Collections.Generic;
using System.Text;

namespace Turbo.Primitives.Moderation;

/// <summary>
/// How a word list is matched against text, shared by the hotel's filter and each room's own: a
/// word is a run of letters and digits, so punctuation, line breaks and the like around it do not
/// hide it ("word!", "(word)"), while a word inside a longer one is left alone. Whether two words
/// are the same is the set's comparer; both lists ignore case.
/// </summary>
public static class FilterWords
{
    /// <summary>The text with every listed word replaced, or the text itself when none is in it.</summary>
    public static string Apply(string text, IReadOnlySet<string> words, string replacement)
    {
        if (words.Count == 0 || text.Length == 0)
            return text;

        StringBuilder? filtered = null;
        var copied = 0;

        foreach (var (start, length) in Words(text))
        {
            if (!words.Contains(text.Substring(start, length)))
                continue;

            filtered ??= new StringBuilder(text.Length);
            filtered.Append(text, copied, start - copied).Append(replacement);
            copied = start + length;
        }

        if (filtered is null)
            return text;

        return filtered.Append(text, copied, text.Length - copied).ToString();
    }

    /// <summary>Whether any listed word is in the text.</summary>
    public static bool ContainsAny(string text, IReadOnlySet<string> words)
    {
        if (words.Count == 0 || text.Length == 0)
            return false;

        foreach (var (start, length) in Words(text))
        {
            if (words.Contains(text.Substring(start, length)))
                return true;
        }

        return false;
    }

    private static IEnumerable<(int Start, int Length)> Words(string text)
    {
        var start = -1;

        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsLetterOrDigit(text[i]))
            {
                if (start < 0)
                    start = i;

                continue;
            }

            if (start >= 0)
            {
                yield return (start, i - start);

                start = -1;
            }
        }

        if (start >= 0)
            yield return (start, text.Length - start);
    }
}
