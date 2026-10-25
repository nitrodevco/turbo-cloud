using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Rooms.Grains.Systems;

public sealed partial class RoomWiredSystem
{
    /// <summary><c>wiredmenu.logs_overview.log_source.1</c>: a line a "write to logs" effect wrote.</summary>
    public const int LOG_SOURCE_WIRED = 1;

    // The client drops a page whose size is not the one it asked for (WiredRoomLogListController
    // .onGetPage), so the size is echoed and only what is read is bounded.
    private const int MAX_ROOM_LOG_PAGE_SIZE = 200;

    private readonly LinkedList<WiredRoomLogEntrySnapshot> _roomLogs = [];
    private long _nextRoomLogId = 1;

    /// <summary>
    /// A "write to logs" action fired. The line goes to the silo log at the requested level
    /// and to the room's own log, the list "View full logs" pages through (AS3
    /// <c>WiredLogEntry</c>: level, source, message, time). It is not one of the monitor's errors,
    /// which are a fixed list (<see cref="GetErrorLogs"/>). The room keeps the latest
    /// <c>WiredConfig.MaxRoomLogEntries</c> lines ("Only the most recent 200 logs are stored per
    /// type", <c>wiredmenu.logs_overview.info</c>).
    /// </summary>
    public void RecordLog(WiredLogLevelType level, string message)
    {
        var logLevel = level switch
        {
            WiredLogLevelType.Debug => LogLevel.Debug,
            WiredLogLevelType.Info => LogLevel.Information,
            WiredLogLevelType.Warning => LogLevel.Warning,
            WiredLogLevelType.Error => LogLevel.Error,
            _ => LogLevel.Debug,
        };

        Logger.Log(logLevel, "Wired log in room {RoomId}: {Message}", _roomGrain.RoomId, message);

        var cap = WiredConfig.MaxRoomLogEntries;

        if (cap <= 0)
            return;

        while (_roomLogs.Count >= cap)
            _roomLogs.RemoveFirst();

        var now = UtcNow;

        _roomLogs.AddLast(
            new WiredRoomLogEntrySnapshot
            {
                Id = _nextRoomLogId++,
                Level = (int)level,
                Source = LOG_SOURCE_WIRED,
                Message = message,
                TimestampMs = now.ToUnixTimeMilliseconds(),
                TimestampText = TimeZoneInfo
                    .ConvertTime(now, GetRoomTimeZone())
                    // Day first, as Habbo's list shows it ("25-10-2026 01:37:45", official
                    // client capture 2026-10-25T01-37-31, W4 lab action.49.R3).
                    .ToString("dd-MM-yyyy HH:mm:ss", CultureInfo.InvariantCulture),
            }
        );
    }

    /// <summary>The room's log lines, oldest first.</summary>
    public ImmutableArray<WiredRoomLogEntrySnapshot> GetRoomLogs() => [.. _roomLogs];

    /// <summary>
    /// One page of the room's log, newest first, narrowed by level, source (-1 for any) and a
    /// text the message contains (case-insensitive). Pages count from 1.
    /// </summary>
    public WiredRoomLogPageSnapshot GetRoomLogPage(
        int page,
        int pageSize,
        int levelFilter,
        int sourceFilter,
        string query
    )
    {
        page = Math.Max(page, 1);
        var take = Math.Clamp(pageSize, 1, MAX_ROOM_LOG_PAGE_SIZE);
        query ??= string.Empty;

        var matching = _roomLogs
            .Reverse()
            .Where(x =>
                (levelFilter < 0 || x.Level == levelFilter)
                && (sourceFilter < 0 || x.Source == sourceFilter)
                && (
                    query.Length == 0
                    || x.Message.Contains(query, StringComparison.OrdinalIgnoreCase)
                )
            )
            .ToList();

        return new WiredRoomLogPageSnapshot
        {
            TotalEntries = matching.Count,
            Page = page,
            PageSize = pageSize,
            Entries = [.. matching.Skip((page - 1) * take).Take(take)],
            LogLevelFilter = levelFilter,
            LogSourceFilter = sourceFilter,
            Query = query,
        };
    }
}
