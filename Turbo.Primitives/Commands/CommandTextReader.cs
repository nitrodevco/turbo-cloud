using System.Text;

namespace Turbo.Primitives.Commands;

/// <summary>Reads a word or a double-quoted string, retaining its source range.</summary>
public static class CommandTextReader
{
    public static CommandToken Read(string text, ref int cursor)
    {
        while (cursor < text.Length && char.IsWhiteSpace(text[cursor]))
            cursor++;
        var start = cursor;
        if (cursor == text.Length)
            return new(string.Empty, start, cursor, true);
        if (text[cursor] != '"')
        {
            while (cursor < text.Length && !char.IsWhiteSpace(text[cursor]))
                cursor++;
            return new(text[start..cursor], start, cursor, true);
        }
        cursor++;
        var value = new StringBuilder();
        while (cursor < text.Length)
        {
            var next = text[cursor++];
            if (next == '"')
                return new(
                    value.ToString(),
                    start,
                    cursor,
                    cursor == text.Length || char.IsWhiteSpace(text[cursor])
                );
            if (next == '\\')
            {
                if (cursor == text.Length || (text[cursor] != '"' && text[cursor] != '\\'))
                    return new(value.ToString(), start, cursor, false);
                next = text[cursor++];
            }
            value.Append(next);
        }
        return new(value.ToString(), start, cursor, false);
    }
}
