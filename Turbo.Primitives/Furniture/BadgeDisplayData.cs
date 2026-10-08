using System;
using System.Collections.Generic;
using System.Text.Json;
using Turbo.Primitives.Furniture.Enums;

namespace Turbo.Primitives.Furniture;

/// <summary>
/// Badge display data as the client reads it: a string array of the state, the badge code, the
/// name of who bought it and the date (<c>FurnitureBadgeDisplayLogic</c> draws index 1, the
/// engraving widget prints 1 to 3). The badge is chosen on the catalog's badge display page and
/// travels as the purchase's extra parameter; the buyer has to own it.
/// </summary>
public static class BadgeDisplayData
{
    /// <summary>
    /// The logic of a badge display. Here rather than in the room module because the catalog
    /// engraves one with its badge before it exists in a room.
    /// </summary>
    public const string LOGIC_NAME = "badge_display";

    /// <summary>The state a display stands in; it has no other.</summary>
    public const string STATE = "0";

    public const int BADGE_CODE_INDEX = 1;

    public static bool IsBadgeDisplay(string? logicName) =>
        string.Equals(logicName, LOGIC_NAME, StringComparison.Ordinal);

    public static string[] Compose(string badgeCode, string ownerName, string date) =>
        [STATE, badgeCode, ownerName, date];

    /// <summary>The extra data a newly bought display starts with.</summary>
    public static string ExtraData(string badgeCode, string ownerName, string date) =>
        JsonSerializer.Serialize(
            new Dictionary<string, object>
            {
                [ExtraDataSectionType.STUFF] = new { Data = Compose(badgeCode, ownerName, date) },
            }
        );
}
