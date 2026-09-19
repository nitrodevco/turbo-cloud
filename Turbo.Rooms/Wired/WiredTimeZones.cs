using System;
using Microsoft.Extensions.Logging;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired;

/// <summary>Resolves the "now" that date and time conditions compare, in a named timezone.</summary>
public static class WiredTimeZones
{
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
            return TimeZoneInfo.ConvertTime(
                DateTimeOffset.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById(timezoneId.Trim())
            );
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
