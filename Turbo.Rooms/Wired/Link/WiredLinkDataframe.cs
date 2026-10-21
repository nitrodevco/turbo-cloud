using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Turbo.Rooms.Wired.Link;

/// <summary>
/// How the dataframe boxes store their rows, as the AS3 editor writes and checks them
/// (WIN63-202610201032 <c>LinkDataframeCodec.as</c>): the text is a JSON array of
/// <c>[key, text]</c> pairs, one per row in use; keys are normalised to lower-case word
/// characters, 1 to 40 of them.
/// </summary>
public static partial class WiredLinkDataframe
{
    public const int MAX_TEXT_BYTES = 3000;
    public const int MAX_MESSAGE_BYTES = 200;
    public const int MAX_KEY_LENGTH = 40;

    public const string INVALID_DATAFRAME = "wiredfurni.error.link.invalid_dataframe";
    public const string KEY_LENGTH = "wiredfurni.error.link.key_length";
    public const string NAME_SYNTAX = "wiredfurni.error.variables.name_syntax";
    public const string NAME_UNIQUE = "wiredfurni.error.variables.name_uniq";
    public const string TOO_LARGE = "wiredfurni.error.link.configuration_too_large";
    public const string DESTINATION_CONFLICT = "wiredfurni.error.link.destination_conflict";
    public const string INVALID_CAPTURE_VARIABLE = "wiredfurni.error.link.invalid_capture_variable";
    public const string PLACEHOLDER_LENGTH = "wiredfurni.error.placeholder.name_length";
    public const string PLACEHOLDER_SYNTAX = "wiredfurni.error.placeholder.name_syntax";
    public const string PLACEHOLDER_RESERVED = "wiredfurni.error.placeholder.reserved";

    /// <summary>The <paramref name="count"/> rows of <paramref name="text"/>, or null when it does not hold exactly that many.</summary>
    public static List<(string Key, string Text)>? Decode(string? text, int count)
    {
        if (count < 0 || count > WiredLinkBoxes.MAX_ROWS)
            return null;

        if (string.IsNullOrEmpty(text))
            return count == 0 ? [] : null;

        try
        {
            using var doc = JsonDocument.Parse(text);

            if (
                doc.RootElement.ValueKind != JsonValueKind.Array
                || doc.RootElement.GetArrayLength() != count
            )
                return null;

            var rows = new List<(string, string)>(count);

            foreach (var row in doc.RootElement.EnumerateArray())
            {
                if (
                    row.ValueKind != JsonValueKind.Array
                    || row.GetArrayLength() != 2
                    || row[0].ValueKind != JsonValueKind.String
                    || row[1].ValueKind != JsonValueKind.String
                )
                    return null;

                rows.Add((row[0].GetString()!, row[1].GetString()!));
            }

            return rows;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Underscores and spaces as one, trimmed, lower case (AS3 <c>normalizeKey</c>).</summary>
    public static string NormalizeKey(string key) =>
        key.Replace('_', ' ').Trim(TrimmedChars).Replace(' ', '_').ToLowerInvariant();

    public static string? KeyError(string key)
    {
        key = NormalizeKey(key);

        if (key.Length == 0 || key.Length > MAX_KEY_LENGTH)
            return KEY_LENGTH;

        return WordRegex().IsMatch(key) ? null : NAME_SYNTAX;
    }

    public static string? PlaceholderError(string name)
    {
        if (name.Length == 0 || name.Length > MAX_KEY_LENGTH)
            return PLACEHOLDER_LENGTH;

        if (!WordRegex().IsMatch(name))
            return PLACEHOLDER_SYNTAX;

        return string.Equals(
            name,
            WiredPlaceholderText.TRIGGER_TEXT_NAME,
            StringComparison.OrdinalIgnoreCase
        )
            ? PLACEHOLDER_RESERVED
            : null;
    }

    /// <summary>
    /// The size limits of a stored dataframe (AS3 <c>storageError</c>): the text at most 3000
    /// UTF-8 bytes, the ints joined with "|" at most 400 characters, the variable ids 240.
    /// </summary>
    public static string? StorageError(
        string text,
        IEnumerable<int> ints,
        IEnumerable<string> ids
    ) =>
        WiredLinkBoxes.Utf8Length(text) > MAX_TEXT_BYTES
        || string.Join("|", ints).Length > 400
        || string.Join("|", ids).Length > 240
            ? TOO_LARGE
            : null;

    /// <summary>The control characters and space the AS3 trims (<c>/^[\x00- ]+|[\x00- ]+$/</c>).</summary>
    private static readonly char[] TrimmedChars =
    [
        .. Enumerable.Range(0, 0x21).Select(x => (char)x),
    ];

    [GeneratedRegex(@"^[A-Za-z0-9_]+$")]
    private static partial Regex WordRegex();
}
