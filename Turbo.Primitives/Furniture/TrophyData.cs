using System;
using Turbo.Primitives.Texts;

namespace Turbo.Primitives.Furniture;

/// <summary>Trophy data as the client reads it: owner, date and message, tab-separated.</summary>
public static class TrophyData
{
    public const char SEPARATOR = '\t';
    public const string DATE_FORMAT = ClientDates.DISPLAY_FORMAT;

    /// <summary>
    /// The logic of an engraved trophy. Here rather than in the room module because the catalog
    /// engraves one bought from the trophy page before it exists in a room.
    /// </summary>
    public const string LOGIC_NAME = "trophy";

    public static bool IsTrophy(string? logicName) =>
        string.Equals(logicName, LOGIC_NAME, StringComparison.Ordinal);

    public static string Compose(string ownerName, string date, string message) =>
        $"{ownerName}{SEPARATOR}{date}{SEPARATOR}{message}";
}
