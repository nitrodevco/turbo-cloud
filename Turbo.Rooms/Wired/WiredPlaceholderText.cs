namespace Turbo.Rooms.Wired;

/// <summary>Parsing of the "name" or "name\tdelimiter" string param of the placeholder addons.</summary>
public static class WiredPlaceholderText
{
    /// <summary>
    /// How a player writes a placeholder in a wired text: its sigil and its name in brackets,
    /// "$(name)" for the text output add-ons and "#(name)" for the capturer
    /// (<c>PlaceholderNameSection</c>: "Use this by typing $(name) in Wired texts").
    /// </summary>
    public static string Token(char sigil, string name) => $"{sigil}({name})";

    /// <summary>The name inside a token: <c>name</c> for <c>$(name)</c>; empty for an empty token.</summary>
    public static string NameOf(string token) => token.Length > 3 ? token[2..^1] : string.Empty;

    /// <summary>
    /// The placeholder "WIRED Trigger: User Says Keyword" creates for its stack, filled with what
    /// was said (Wired Faculty announcement, 12/10/2026: "Placeholder $(trigger_text)
    /// automatically created for WIRED Trigger: User Says Keyword").
    /// </summary>
    public const string TRIGGER_TEXT_NAME = "trigger_text";

    /// <summary><c>$(trigger_text)</c>, as a text writes it.</summary>
    public static readonly string TriggerTextToken = Token('$', TRIGGER_TEXT_NAME);

    private const string DEFAULT_DELIMITER = ", ";

    public static (string name, string delimiter) SplitNameAndDelimiter(string? stringParam)
    {
        if (string.IsNullOrWhiteSpace(stringParam))
            return (string.Empty, DEFAULT_DELIMITER);

        var index = stringParam.IndexOf('\t');

        if (index < 0)
            return (stringParam.Trim(), DEFAULT_DELIMITER);

        var delimiter = stringParam[(index + 1)..];

        return (stringParam[..index].Trim(), delimiter.Length == 0 ? DEFAULT_DELIMITER : delimiter);
    }
}
