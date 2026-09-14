using System;
using Microsoft.Extensions.Logging;

namespace Turbo.Rooms.Grains.Systems;

public sealed partial class RoomWiredSystem
{
    private string? _cachedTimezoneId;
    private TimeZoneInfo _cachedTimezone = TimeZoneInfo.Utc;

    /// <summary>
    /// The current time in the room's configured wired timezone. Date and time based wired
    /// evaluate against this rather than the server clock, so a room set to Helsinki triggers
    /// at Helsinki midnight regardless of where the silo runs.
    /// </summary>
    public DateTimeOffset GetRoomLocalTime() =>
        TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, GetRoomTimeZone());

    public TimeZoneInfo GetRoomTimeZone()
    {
        var timezoneId = _roomGrain._state.RoomSnapshot.WiredTimezone;

        if (string.Equals(timezoneId, _cachedTimezoneId, StringComparison.Ordinal))
            return _cachedTimezone;

        _cachedTimezoneId = timezoneId;
        _cachedTimezone = TimeZoneInfo.Utc;

        if (string.IsNullOrWhiteSpace(timezoneId))
            return _cachedTimezone;

        try
        {
            _cachedTimezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            _roomGrain._logger.LogWarning(
                ex,
                "Unknown wired timezone {Timezone} for room {RoomId}; falling back to UTC",
                timezoneId,
                _roomGrain.RoomId
            );
        }

        return _cachedTimezone;
    }
}
