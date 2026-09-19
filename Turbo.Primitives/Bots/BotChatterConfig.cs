using System;
using System.Globalization;

namespace Turbo.Primitives.Bots;

/// <summary>
/// The chat setup string the client's chatter configuration exchanges with the server:
/// <c>text;#;autoChat;#;delay;#;mixSentences</c>. The client strips the separator from the
/// text before sending and accepts a legacy three-field form split on <c>;</c> when reading.
/// </summary>
public static class BotChatterConfig
{
    public const string SEPARATOR = ";#;";
    private const int FIELD_COUNT = 4;

    public static string Compose(string text, bool autoChat, int delaySeconds, bool mixSentences) =>
        string.Join(
            SEPARATOR,
            text.Replace(SEPARATOR, " ", StringComparison.Ordinal),
            autoChat ? "true" : "false",
            delaySeconds.ToString(CultureInfo.InvariantCulture),
            mixSentences ? "true" : "false"
        );

    public static bool TryParse(
        string? data,
        out string text,
        out bool autoChat,
        out int delaySeconds,
        out bool mixSentences
    )
    {
        text = string.Empty;
        autoChat = false;
        delaySeconds = 0;
        mixSentences = false;

        if (data is null)
            return false;

        var fields = data.Split(SEPARATOR);

        if (fields.Length != FIELD_COUNT)
            return false;

        if (!TryParseFlag(fields[1], out autoChat) || !TryParseFlag(fields[3], out mixSentences))
            return false;

        if (
            !int.TryParse(
                fields[2],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out delaySeconds
            )
        )
            return false;

        text = fields[0];

        return true;
    }

    private static bool TryParseFlag(string value, out bool flag)
    {
        flag = false;

        switch (value.Trim().ToLowerInvariant())
        {
            case "true":
            case "1":
                flag = true;
                return true;
            case "false":
            case "0":
                return true;
            default:
                return false;
        }
    }
}
