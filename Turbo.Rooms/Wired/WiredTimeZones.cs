using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired;

/// <summary>Resolves the "now" that date and time conditions compare, in a named timezone.</summary>
public static class WiredTimeZones
{
    /// <summary>
    /// Habbo's <c>wired.timezones</c> list names several zones by their legacy IANA links
    /// (<c>US/Central</c>, <c>Canada/Eastern</c>, ...). Linux hosts whose tzdata ships without the
    /// legacy links (Ubuntu 24.04 moved them to <c>tzdata-legacy</c>) cannot find those names, so
    /// each one is also tried as the zone it links to.
    /// </summary>
    private static readonly FrozenDictionary<string, string> LegacyLinks = new Dictionary<
        string,
        string
    >(StringComparer.OrdinalIgnoreCase)
    {
        ["Canada/Atlantic"] = "America/Halifax",
        ["Canada/Central"] = "America/Winnipeg",
        ["Canada/Eastern"] = "America/Toronto",
        ["Canada/Mountain"] = "America/Edmonton",
        ["Canada/Newfoundland"] = "America/St_Johns",
        ["Canada/Pacific"] = "America/Vancouver",
        ["Canada/Saskatchewan"] = "America/Regina",
        ["Canada/Yukon"] = "America/Whitehorse",
        ["US/Alaska"] = "America/Anchorage",
        ["US/Aleutian"] = "America/Adak",
        ["US/Arizona"] = "America/Phoenix",
        ["US/Central"] = "America/Chicago",
        ["US/East-Indiana"] = "America/Indiana/Indianapolis",
        ["US/Eastern"] = "America/New_York",
        ["US/Hawaii"] = "Pacific/Honolulu",
        ["US/Indiana-Starke"] = "America/Indiana/Knox",
        ["US/Michigan"] = "America/Detroit",
        ["US/Mountain"] = "America/Denver",
        ["US/Pacific"] = "America/Los_Angeles",
        ["US/Samoa"] = "Pacific/Pago_Pago",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The zone a wired timezone name means: the system zone of that id, else the zone a legacy
    /// link of that name points to. Throws <see cref="TimeZoneNotFoundException"/> when neither exists.
    /// </summary>
    public static TimeZoneInfo Find(string timezoneId)
    {
        var id = timezoneId.Trim();

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException) when (LegacyLinks.TryGetValue(id, out var target))
        {
            return TimeZoneInfo.FindSystemTimeZoneById(target);
        }
    }

    /// <summary>
    /// Local time in the timezone named by a box, falling back to the room wired timezone when
    /// the name is empty or unknown.
    /// </summary>
    public static DateTimeOffset GetLocalTime(RoomGrain roomGrain, string? timezoneId)
    {
        if (string.IsNullOrWhiteSpace(timezoneId))
            return roomGrain.WiredSystem.GetRoomLocalTime();

        try
        {
            return TimeZoneInfo.ConvertTime(roomGrain.UtcNow, Find(timezoneId));
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            roomGrain._logger.LogDebug(
                ex,
                "Unknown wired timezone {Timezone} in room {RoomId}; using the room timezone",
                timezoneId,
                roomGrain.RoomId
            );

            return roomGrain.WiredSystem.GetRoomLocalTime();
        }
    }
}
