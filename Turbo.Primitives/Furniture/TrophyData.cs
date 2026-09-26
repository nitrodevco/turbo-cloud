using Turbo.Primitives.Texts;

namespace Turbo.Primitives.Furniture;

/// <summary>Trophy data as the client reads it: owner, date and message, tab-separated.</summary>
public static class TrophyData
{
    public const char SEPARATOR = '\t';
    public const string DATE_FORMAT = ClientDates.DISPLAY_FORMAT;

    public static string Compose(string ownerName, string date, string message) =>
        $"{ownerName}{SEPARATOR}{date}{SEPARATOR}{message}";
}
