using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;
using Turbo.Database.Entities.Tracking;

namespace Turbo.Database.Tracking;

/// <summary>
/// One day of raw client telemetry made into its daily figures (<see cref="ClientTelemetryKinds"/>):
/// counts, sums, different players, the median, 95th percentile and highest of each group. The
/// maintenance service stores a finished day's; the admin panel makes today's the same way.
/// </summary>
public static class ClientTelemetryRollups
{
    /// <summary>The figures of <paramref name="day"/> (UTC), without the day marker.</summary>
    public static async Task<List<ClientTelemetryDailyEntity>> BuildDayAsync(
        TurboDbContext db,
        DateOnly day,
        CancellationToken ct
    )
    {
        var from = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = from.AddDays(1);
        var rows = new List<ClientTelemetryDailyEntity>();

        var performance = await db
            .ClientPerformanceLogs.AsNoTracking()
            .Where(x => x.CreatedAt >= from && x.CreatedAt < to)
            .Select(x => new
            {
                x.PlayerId,
                x.RoomId,
                x.BrowserFamily,
                x.OsFamily,
                x.DeviceType,
                x.MemoryKb,
                x.AverageUpdateIntervalMs,
                x.SlowUpdates,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var g in performance.GroupBy(x => (x.BrowserFamily, x.OsFamily, x.DeviceType)))
        {
            var (browser, os, device) = g.Key;
            var withFrames = g.Where(x => x.AverageUpdateIntervalMs > 0).ToList();

            if (withFrames.Count > 0)
                rows.Add(
                    Figure(
                        day,
                        ClientTelemetryKinds.FPS,
                        browser,
                        os,
                        device,
                        withFrames.Select(x => Fps(x.AverageUpdateIntervalMs)),
                        withFrames.Select(x => x.PlayerId)
                    )
                );

            rows.Add(
                Figure(
                    day,
                    ClientTelemetryKinds.MEMORY,
                    browser,
                    os,
                    device,
                    g.Select(x => x.MemoryKb / 1024),
                    g.Select(x => x.PlayerId)
                )
            );
            rows.Add(
                Figure(
                    day,
                    ClientTelemetryKinds.SLOW_UPDATES,
                    browser,
                    os,
                    device,
                    g.Select(x => x.SlowUpdates),
                    g.Select(x => x.PlayerId)
                )
            );
        }

        var latency = await db
            .ClientLatencyReports.AsNoTracking()
            .Where(x => x.CreatedAt >= from && x.CreatedAt < to)
            .Select(x => new
            {
                x.PlayerId,
                x.RoomId,
                x.BrowserFamily,
                x.OsFamily,
                x.DeviceType,
                x.AverageMs,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var g in latency.GroupBy(x => (x.BrowserFamily, x.OsFamily, x.DeviceType)))
            rows.Add(
                Figure(
                    day,
                    ClientTelemetryKinds.LATENCY,
                    g.Key.BrowserFamily,
                    g.Key.OsFamily,
                    g.Key.DeviceType,
                    g.Select(x => x.AverageMs),
                    g.Select(x => x.PlayerId)
                )
            );

        foreach (var g in latency.Where(x => x.RoomId is not null).GroupBy(x => x.RoomId!.Value))
            rows.Add(
                Figure(
                    day,
                    ClientTelemetryKinds.ROOM_LATENCY,
                    Room(g.Key),
                    "",
                    "",
                    g.Select(x => x.AverageMs),
                    g.Select(x => x.PlayerId)
                )
            );

        // A room's frame rate: from the performance reports sent in it and the fps events.
        var fpsEvents = await db
            .ClientEventLogs.AsNoTracking()
            .Where(x =>
                x.CreatedAt >= from
                && x.CreatedAt < to
                && x.Category == ClientTelemetryKinds.CLIENT_PERFORMANCE_CATEGORY
                && x.RoomId != null
            )
            .Select(x => new
            {
                x.PlayerId,
                x.RoomId,
                x.Type,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var roomFps = performance
            .Where(x => x.RoomId is not null && x.AverageUpdateIntervalMs > 0)
            .Select(x => (Room: x.RoomId!.Value, x.PlayerId, Fps: Fps(x.AverageUpdateIntervalMs)))
            .Concat(
                fpsEvents
                    .Where(x =>
                        int.TryParse(x.Type, CultureInfo.InvariantCulture, out var fps) && fps >= 0
                    )
                    .Select(x =>
                        (
                            Room: x.RoomId!.Value,
                            x.PlayerId,
                            Fps: int.Parse(x.Type, CultureInfo.InvariantCulture)
                        )
                    )
            );

        foreach (var g in roomFps.GroupBy(x => x.Room))
            rows.Add(
                Figure(
                    day,
                    ClientTelemetryKinds.ROOM_FPS,
                    Room(g.Key),
                    "",
                    "",
                    g.Select(x => x.Fps),
                    g.Select(x => x.PlayerId)
                )
            );

        var lag = await db
            .ClientLagWarnings.AsNoTracking()
            .Where(x => x.CreatedAt >= from && x.CreatedAt < to)
            .Select(x => new
            {
                x.PlayerId,
                x.RoomId,
                x.WarningCount,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (lag.Count > 0)
            rows.Add(
                Figure(
                    day,
                    ClientTelemetryKinds.LAG,
                    "",
                    "",
                    "",
                    lag.Select(x => x.WarningCount),
                    lag.Select(x => x.PlayerId)
                )
            );

        foreach (var g in lag.Where(x => x.RoomId is not null).GroupBy(x => x.RoomId!.Value))
            rows.Add(
                Figure(
                    day,
                    ClientTelemetryKinds.ROOM_LAG,
                    Room(g.Key),
                    "",
                    "",
                    g.Select(x => x.WarningCount),
                    g.Select(x => x.PlayerId)
                )
            );

        // Events are the many rows: counted by the database.
        var events = await db
            .ClientEventLogs.AsNoTracking()
            .Where(x =>
                x.CreatedAt >= from
                && x.CreatedAt < to
                && x.Category != ClientTelemetryKinds.CLIENT_PERFORMANCE_CATEGORY
            )
            .GroupBy(x => new
            {
                x.Category,
                x.Type,
                x.Action,
            })
            .Select(g => new
            {
                g.Key.Category,
                g.Key.Type,
                g.Key.Action,
                Count = g.Count(),
                Players = g.Select(x => x.PlayerId).Distinct().Count(),
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        rows.AddRange(
            events.Select(x => new ClientTelemetryDailyEntity
            {
                Day = day,
                Kind = ClientTelemetryKinds.EVENT,
                Key1 = x.Category,
                Key2 = x.Type,
                Key3 = x.Action,
                Samples = x.Count,
                Players = x.Players,
                Sum = x.Count,
                P50 = 0,
                P95 = 0,
                Max = 0,
            })
        );

        var steps = await db
            .ClientLoginSteps.AsNoTracking()
            .Where(x => x.CreatedAt >= from && x.CreatedAt < to)
            .Select(x => new
            {
                x.PlayerId,
                x.Step,
                x.MsSinceConnect,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var g in steps.GroupBy(x => x.Step))
            rows.Add(
                Figure(
                    day,
                    ClientTelemetryKinds.LOGIN,
                    g.Key,
                    "",
                    "",
                    g.Select(x => x.MsSinceConnect),
                    g.Where(x => x.PlayerId is not null).Select(x => x.PlayerId!.Value)
                )
            );

        return rows;
    }

    /// <summary>The frame rate an average time between frames makes.</summary>
    public static int Fps(int averageUpdateIntervalMs) =>
        averageUpdateIntervalMs <= 0 ? 0 : (int)Math.Round(1000.0 / averageUpdateIntervalMs);

    /// <summary>The value at <paramref name="percent"/> of sorted values (nearest rank).</summary>
    public static int Percentile(IReadOnlyList<int> sorted, int percent) =>
        sorted.Count == 0
            ? 0
            : sorted[
                Math.Clamp(
                    (int)Math.Ceiling(percent / 100.0 * sorted.Count) - 1,
                    0,
                    sorted.Count - 1
                )
            ];

    private static string Room(int roomId) => roomId.ToString(CultureInfo.InvariantCulture);

    private static ClientTelemetryDailyEntity Figure(
        DateOnly day,
        string kind,
        string key1,
        string key2,
        string key3,
        IEnumerable<int> values,
        IEnumerable<int> players
    )
    {
        var sorted = values.Order().ToList();

        return new ClientTelemetryDailyEntity
        {
            Day = day,
            Kind = kind,
            Key1 = key1,
            Key2 = key2,
            Key3 = key3,
            Samples = sorted.Count,
            Players = players.Distinct().Count(),
            Sum = sorted.Sum(x => (long)x),
            P50 = Percentile(sorted, 50),
            P95 = Percentile(sorted, 95),
            Max = sorted.Count == 0 ? 0 : sorted[^1],
        };
    }
}
