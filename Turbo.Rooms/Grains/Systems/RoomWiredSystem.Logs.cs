using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Rooms.Grains.Systems;

public sealed partial class RoomWiredSystem
{
    private readonly Queue<WiredRoomLogLine> _roomLogs = [];

    /// <summary>
    /// A "write to logs" action fired. The line goes to the silo log at the requested level
    /// and to the room's own log, the list Habbo's "View full logs" pages through (AS3
    /// <c>WiredLogEntry</c>: level, source, message, time). It is not one of the monitor's errors,
    /// which are a fixed list (<see cref="GetErrorLogs"/>). The oldest line makes room once
    /// <c>WiredConfig.MaxRoomLogEntries</c> are kept.
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

        var cap = _roomGrain._wiredConfig.MaxRoomLogEntries;

        if (cap <= 0)
            return;

        while (_roomLogs.Count >= cap)
            _roomLogs.Dequeue();

        _roomLogs.Enqueue(new WiredRoomLogLine(level, message, now));
    }

    /// <summary>The room's log lines, oldest first.</summary>
    public ImmutableArray<WiredRoomLogLine> GetRoomLogs() => [.. _roomLogs];
}

public sealed record WiredRoomLogLine(WiredLogLevelType Level, string Message, long TimestampMs);
