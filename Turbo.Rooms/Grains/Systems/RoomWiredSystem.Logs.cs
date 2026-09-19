using Microsoft.Extensions.Logging;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Rooms.Grains.Systems;

public sealed partial class RoomWiredSystem
{
    /// <summary>
    /// A "write to logs" action fired. The line goes to the silo log at the requested level
    /// and to the wired monitor list of the room, where it is grouped with the faults.
    /// </summary>
    public void RecordLog(WiredLogLevelType level, string message, long now)
    {
        var logLevel = level switch
        {
            WiredLogLevelType.Debug => LogLevel.Debug,
            WiredLogLevelType.Info => LogLevel.Information,
            WiredLogLevelType.Warning => LogLevel.Warning,
            WiredLogLevelType.Error => LogLevel.Error,
            _ => LogLevel.Debug,
        };

        _roomGrain._logger.Log(
            logLevel,
            "Wired log in room {RoomId}: {Message}",
            _roomGrain.RoomId,
            message
        );

        RecordError(message, $"Log {level}", now);
    }
}
