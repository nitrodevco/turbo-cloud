using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Turbo.Admin.Api.Contracts;
using Turbo.Database.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Entities.Tracking;
using Turbo.Database.Tracking;
using Turbo.Primitives.Tracking;

namespace Turbo.Admin.Telemetry;

/// <summary>
/// The Client health and Usage pages' figures. Overviews read the daily figures
/// (<c>client_telemetry_daily</c>), and make the days not rolled up yet (today) from the raw rows
/// the same way, so a range may reach back as far as the figures are kept. A figure over several
/// days adds counts and weights each day's percentiles by its samples. A player's or a room's raw
/// reports, and funnels, read the raw rows, so they reach back
/// <see cref="ClientTelemetryConfig.RawRetentionDays"/>. Read-only.
/// </summary>
public sealed class AdminClientTelemetry(
    IDbContextFactory<TurboDbContext> database,
    IOptionsMonitor<ClientTelemetryConfig> config,
    TimeProvider timeProvider
)
{
    /// <summary>The longest range asked for, in days.</summary>
    public const int MAX_DAYS = 366;

    /// <summary>The raw rows a player's or a room's page lists of each kind.</summary>
    public const int ROW_LIMIT = 100;

    /// <summary>The rooms and events an overview lists.</summary>
    public const int TOP_LIMIT = 50;

    /// <summary>The most steps a funnel has.</summary>
    public const int MAX_FUNNEL_STEPS = 6;

    public async Task<ClientHealthResponse> HealthAsync(
        DateOnly? from,
        DateOnly? to,
        string? browser,
        string? os,
        string? device,
        int? room,
        CancellationToken ct
    )
    {
        var (first, last) = Range(from, to);
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var figures = await FiguresAsync(db, first, last, ct).ConfigureAwait(false);
        var roomKey = room?.ToString(System.Globalization.CultureInfo.InvariantCulture);

        bool Matches(ClientTelemetryDailyEntity x) =>
            (string.IsNullOrEmpty(browser) || x.Key1 == browser)
            && (string.IsNullOrEmpty(os) || x.Key2 == os)
            && (string.IsNullOrEmpty(device) || x.Key3 == device);

        var deviceFigures = figures
            .Where(x =>
                x.Kind
                    is ClientTelemetryKinds.FPS
                        or ClientTelemetryKinds.MEMORY
                        or ClientTelemetryKinds.LATENCY
                        or ClientTelemetryKinds.SLOW_UPDATES
            )
            .ToList();
        var filtered = deviceFigures.Where(Matches).ToList();

        var days = Days(first, last)
            .Select(day =>
            {
                var ofDay = figures.Where(x => x.Day == day).ToList();

                if (roomKey is not null)
                    return DayOf(
                        day,
                        ofDay.Where(x =>
                            x.Kind == ClientTelemetryKinds.ROOM_FPS && x.Key1 == roomKey
                        ),
                        [],
                        ofDay.Where(x =>
                            x.Kind == ClientTelemetryKinds.ROOM_LATENCY && x.Key1 == roomKey
                        ),
                        ofDay.Where(x =>
                            x.Kind == ClientTelemetryKinds.ROOM_LAG && x.Key1 == roomKey
                        )
                    );

                var mine = filtered.Where(x => x.Day == day).ToList();

                return DayOf(
                    day,
                    mine.Where(x => x.Kind == ClientTelemetryKinds.FPS),
                    mine.Where(x => x.Kind == ClientTelemetryKinds.MEMORY),
                    mine.Where(x => x.Kind == ClientTelemetryKinds.LATENCY),
                    ofDay.Where(x => x.Kind == ClientTelemetryKinds.LAG)
                );
            })
            .ToArray();

        var devices = filtered
            .GroupBy(x => (x.Key1, x.Key2, x.Key3))
            .Select(g =>
            {
                var fps = Combine(g.Where(x => x.Kind == ClientTelemetryKinds.FPS));
                var memory = Combine(g.Where(x => x.Kind == ClientTelemetryKinds.MEMORY));
                var latency = Combine(g.Where(x => x.Kind == ClientTelemetryKinds.LATENCY));
                var slow = Combine(g.Where(x => x.Kind == ClientTelemetryKinds.SLOW_UPDATES));

                return new ClientHealthDevice(
                    g.Key.Key1,
                    g.Key.Key2,
                    g.Key.Key3,
                    memory.Samples,
                    memory.Players,
                    fps.P50,
                    fps.P95,
                    memory.P50,
                    memory.P95,
                    latency.Samples,
                    latency.P50,
                    latency.P95,
                    slow.Sum
                );
            })
            .OrderByDescending(x => x.Reports + x.LatencyReports)
            .ToArray();

        var roomFigures = figures
            .Where(x =>
                x.Kind
                    is ClientTelemetryKinds.ROOM_FPS
                        or ClientTelemetryKinds.ROOM_LATENCY
                        or ClientTelemetryKinds.ROOM_LAG
                && (roomKey is null || x.Key1 == roomKey)
            )
            .GroupBy(x => x.Key1)
            .Select(g =>
            {
                var fps = Combine(g.Where(x => x.Kind == ClientTelemetryKinds.ROOM_FPS));
                var latency = Combine(g.Where(x => x.Kind == ClientTelemetryKinds.ROOM_LATENCY));
                var lag = Combine(g.Where(x => x.Kind == ClientTelemetryKinds.ROOM_LAG));

                return (
                    RoomId: int.TryParse(g.Key, out var id) ? id : 0,
                    Lag: lag,
                    Fps: fps,
                    Latency: latency,
                    PlayerDays: Math.Max(fps.Players, Math.Max(latency.Players, lag.Players))
                );
            })
            .Where(x => x.RoomId > 0)
            .OrderByDescending(x => x.Lag.Sum)
            .ThenByDescending(x => x.Fps.Samples + x.Latency.Samples)
            .Take(TOP_LIMIT)
            .ToList();

        var roomNames = await RoomNamesAsync(db, roomFigures.Select(x => x.RoomId), ct)
            .ConfigureAwait(false);

        var rooms = roomFigures
            .Select(x => new ClientHealthRoom(
                x.RoomId,
                roomNames.GetValueOrDefault(x.RoomId),
                x.Lag.Sum,
                x.Lag.Samples,
                x.Fps.Samples,
                x.Fps.P50,
                x.Fps.P95,
                x.Latency.Samples,
                x.Latency.P50,
                x.PlayerDays
            ))
            .ToArray();

        var login = ClientLoginSteps
            .Ordered.Select(step =>
            {
                var figure = Combine(
                    figures.Where(x => x.Kind == ClientTelemetryKinds.LOGIN && x.Key1 == step)
                );

                return new ClientLoginStepFigure(step, figure.Samples, figure.P50, figure.P95);
            })
            .ToArray();

        return new ClientHealthResponse(
            first,
            last,
            config.CurrentValue.RawRetentionDays,
            days,
            devices,
            rooms,
            login,
            new ClientTelemetryFilters(
                [.. deviceFigures.Select(x => x.Key1).Distinct().Order()],
                [.. deviceFigures.Select(x => x.Key2).Distinct().Order()],
                [.. deviceFigures.Select(x => x.Key3).Distinct().Order()]
            )
        );
    }

    public async Task<ClientUsageResponse> UsageAsync(
        DateOnly? from,
        DateOnly? to,
        string? category,
        CancellationToken ct
    )
    {
        var (first, last) = Range(from, to);
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var events = (await FiguresAsync(db, first, last, ct).ConfigureAwait(false))
            .Where(x => x.Kind == ClientTelemetryKinds.EVENT)
            .ToList();
        var shown = string.IsNullOrEmpty(category)
            ? events
            : events.Where(x => x.Key1 == category).ToList();

        return new ClientUsageResponse(
            first,
            last,
            config.CurrentValue.RawRetentionDays,
            [
                .. Days(first, last)
                    .Select(day =>
                    {
                        var ofDay = shown.Where(x => x.Day == day).ToList();

                        // A player who logged two kinds of event is one of the day's players at
                        // least; the most players of any one event is the closest the figures say.
                        return new ClientUsageDay(
                            day,
                            ofDay.Sum(x => (long)x.Samples),
                            ofDay.Select(x => x.Players).DefaultIfEmpty(0).Max()
                        );
                    }),
            ],
            [
                .. shown
                    .GroupBy(x => (x.Key1, x.Key2, x.Key3))
                    .Select(g => new ClientUsageEvent(
                        g.Key.Key1,
                        g.Key.Key2,
                        g.Key.Key3,
                        g.Sum(x => (long)x.Samples),
                        g.Sum(x => x.Players)
                    ))
                    .OrderByDescending(x => x.Count)
                    .Take(TOP_LIMIT),
            ],
            [
                .. events
                    .GroupBy(x => x.Key1)
                    .Select(g => new ClientUsageCategory(
                        g.Key,
                        g.Sum(x => (long)x.Samples),
                        g.Sum(x => x.Players)
                    ))
                    .OrderByDescending(x => x.Count),
            ]
        );
    }

    /// <summary>
    /// The players who logged each event of <paramref name="steps"/> and all the ones before it,
    /// in the range; each step is <c>category|type|action</c>, an empty or <c>*</c> part matching
    /// any. Only raw rows count, so a range reaches back the raw retention at most.
    /// </summary>
    public async Task<IReadOnlyList<ClientFunnelStep>> FunnelAsync(
        DateOnly? from,
        DateOnly? to,
        IReadOnlyList<string> steps,
        CancellationToken ct
    )
    {
        var (first, last) = Range(from, to);
        var (start, end) = Bounds(first, last);
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var result = new List<ClientFunnelStep>();
        HashSet<int>? remaining = null;

        foreach (var step in steps.Where(x => !string.IsNullOrWhiteSpace(x)).Take(MAX_FUNNEL_STEPS))
        {
            var parts = step.Split('|');
            var category = Part(parts, 0);
            var type = Part(parts, 1);
            var action = Part(parts, 2);

            var query = db
                .ClientEventLogs.AsNoTracking()
                .Where(x => x.CreatedAt >= start && x.CreatedAt < end);

            if (category is not null)
                query = query.Where(x => x.Category == category);

            if (type is not null)
                query = query.Where(x => x.Type == type);

            if (action is not null)
                query = query.Where(x => x.Action == action);

            var players = await query
                .Select(x => x.PlayerId)
                .Distinct()
                .ToListAsync(ct)
                .ConfigureAwait(false);

            remaining = remaining is null ? [.. players] : [.. players.Where(remaining.Contains)];
            result.Add(
                new ClientFunnelStep(category ?? "*", type ?? "*", action ?? "*", remaining.Count)
            );
        }

        return result;
    }

    public async Task<ClientTelemetryPlayerResponse> PlayerAsync(
        int playerId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct
    )
    {
        var (first, last) = Range(from, to);
        var (start, end) = Bounds(first, last);
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var performance = await db
            .ClientPerformanceLogs.AsNoTracking()
            .Where(x => x.PlayerId == playerId && x.CreatedAt >= start && x.CreatedAt < end)
            .OrderByDescending(x => x.Id)
            .Take(ROW_LIMIT)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var latency = await db
            .ClientLatencyReports.AsNoTracking()
            .Where(x => x.PlayerId == playerId && x.CreatedAt >= start && x.CreatedAt < end)
            .OrderByDescending(x => x.Id)
            .Take(ROW_LIMIT)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var lag = await db
            .ClientLagWarnings.AsNoTracking()
            .Where(x => x.PlayerId == playerId && x.CreatedAt >= start && x.CreatedAt < end)
            .OrderByDescending(x => x.Id)
            .Take(ROW_LIMIT)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var events = await db
            .ClientEventLogs.AsNoTracking()
            .Where(x => x.PlayerId == playerId && x.CreatedAt >= start && x.CreatedAt < end)
            .OrderByDescending(x => x.Id)
            .Take(ROW_LIMIT)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var logins = await db
            .ClientLoginSteps.AsNoTracking()
            .Where(x => x.PlayerId == playerId && x.CreatedAt >= start && x.CreatedAt < end)
            .OrderByDescending(x => x.Id)
            .Take(ROW_LIMIT)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var playerName = (
            await PlayerNamesAsync(db, [playerId], ct).ConfigureAwait(false)
        ).GetValueOrDefault(playerId);
        var roomNames = await RoomNamesAsync(
                db,
                performance
                    .Select(x => x.RoomId)
                    .Concat(latency.Select(x => x.RoomId))
                    .Concat(lag.Select(x => x.RoomId))
                    .Concat(events.Select(x => x.RoomId))
                    .Where(x => x is not null)
                    .Select(x => x!.Value),
                ct
            )
            .ConfigureAwait(false);
        var players = new Dictionary<int, string> { [playerId] = playerName ?? "" };

        return new ClientTelemetryPlayerResponse(
            playerId,
            playerName,
            [.. performance.Select(x => PerformanceRow(x, players, roomNames))],
            [.. latency.Select(x => LatencyRow(x, players, roomNames))],
            [.. lag.Select(x => LagRow(x, players, roomNames))],
            [
                .. events.Select(x => new ClientEventRow(
                    x.CreatedAt,
                    x.RoomId,
                    x.RoomId is { } room ? roomNames.GetValueOrDefault(room) : null,
                    x.Category,
                    x.Type,
                    x.Action,
                    x.ExtraString,
                    x.ExtraInt
                )),
            ],
            [.. logins.Select(x => new ClientLoginRow(x.CreatedAt, x.Step, x.MsSinceConnect))]
        );
    }

    public async Task<ClientTelemetryRoomResponse> RoomAsync(
        int roomId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct
    )
    {
        var health = await HealthAsync(from, to, null, null, null, roomId, ct)
            .ConfigureAwait(false);
        var (start, end) = Bounds(health.From, health.To);
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var performance = await db
            .ClientPerformanceLogs.AsNoTracking()
            .Where(x => x.RoomId == roomId && x.CreatedAt >= start && x.CreatedAt < end)
            .OrderByDescending(x => x.Id)
            .Take(ROW_LIMIT)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var latency = await db
            .ClientLatencyReports.AsNoTracking()
            .Where(x => x.RoomId == roomId && x.CreatedAt >= start && x.CreatedAt < end)
            .OrderByDescending(x => x.Id)
            .Take(ROW_LIMIT)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var lag = await db
            .ClientLagWarnings.AsNoTracking()
            .Where(x => x.RoomId == roomId && x.CreatedAt >= start && x.CreatedAt < end)
            .OrderByDescending(x => x.Id)
            .Take(ROW_LIMIT)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var players = await PlayerNamesAsync(
                db,
                performance
                    .Select(x => x.PlayerId)
                    .Concat(latency.Select(x => x.PlayerId))
                    .Concat(lag.Select(x => x.PlayerId)),
                ct
            )
            .ConfigureAwait(false);
        var rooms = await RoomNamesAsync(db, [roomId], ct).ConfigureAwait(false);

        return new ClientTelemetryRoomResponse(
            roomId,
            rooms.GetValueOrDefault(roomId),
            health.Days,
            [.. performance.Select(x => PerformanceRow(x, players, rooms))],
            [.. latency.Select(x => LatencyRow(x, players, rooms))],
            [.. lag.Select(x => LagRow(x, players, rooms))]
        );
    }

    /// <summary>
    /// The daily figures of every day in the range: stored for the days rolled up, made from the
    /// raw rows for the days that are not (today, or a day the maintenance has not reached).
    /// </summary>
    private async Task<List<ClientTelemetryDailyEntity>> FiguresAsync(
        TurboDbContext db,
        DateOnly first,
        DateOnly last,
        CancellationToken ct
    )
    {
        var stored = await db
            .ClientTelemetryDaily.AsNoTracking()
            .Where(x => x.Day >= first && x.Day <= last)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var rolled = stored
            .Where(x => x.Kind == ClientTelemetryKinds.DAY)
            .Select(x => x.Day)
            .ToHashSet();
        var figures = stored.Where(x => x.Kind != ClientTelemetryKinds.DAY).ToList();
        var rawSince = RawSince();

        foreach (var day in Days(first, last))
        {
            if (rolled.Contains(day) || day < rawSince)
                continue;

            figures.AddRange(
                await ClientTelemetryRollups.BuildDayAsync(db, day, ct).ConfigureAwait(false)
            );
        }

        return figures;
    }

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

    private DateOnly RawSince() =>
        config.CurrentValue.RawRetentionDays > 0
            ? Today().AddDays(-config.CurrentValue.RawRetentionDays)
            : DateOnly.MinValue;

    /// <summary>The range asked for, the last week when not, clamped to today and <see cref="MAX_DAYS"/>.</summary>
    private (DateOnly First, DateOnly Last) Range(DateOnly? from, DateOnly? to)
    {
        var today = Today();
        var last = to is { } t && t < today ? t : today;
        var first = from ?? last.AddDays(-6);

        if (first > last)
            first = last;

        if (last.DayNumber - first.DayNumber >= MAX_DAYS)
            first = last.AddDays(-(MAX_DAYS - 1));

        return (first, last);
    }

    private static (DateTime Start, DateTime End) Bounds(DateOnly first, DateOnly last) =>
        (
            first.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            last.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        );

    private static IEnumerable<DateOnly> Days(DateOnly first, DateOnly last)
    {
        for (var day = first; day <= last; day = day.AddDays(1))
            yield return day;
    }

    private static string? Part(string[] parts, int index) =>
        index < parts.Length && parts[index].Trim() is { Length: > 0 } part && part != "*"
            ? part
            : null;

    private static ClientHealthDay DayOf(
        DateOnly day,
        IEnumerable<ClientTelemetryDailyEntity> fps,
        IEnumerable<ClientTelemetryDailyEntity> memory,
        IEnumerable<ClientTelemetryDailyEntity> latency,
        IEnumerable<ClientTelemetryDailyEntity> lag
    )
    {
        var f = Combine(fps);
        var m = Combine(memory);
        var l = Combine(latency);
        var g = Combine(lag);

        return new ClientHealthDay(
            day,
            Math.Max(f.Samples, m.Samples),
            Math.Max(f.Players, Math.Max(m.Players, l.Players)),
            f.P50,
            f.P95,
            m.P50,
            m.P95,
            l.Samples,
            l.P50,
            l.P95,
            g.Sum
        );
    }

    /// <summary>Daily figures as one: counts and sums added, percentiles weighted by samples.</summary>
    private static Combined Combine(IEnumerable<ClientTelemetryDailyEntity> figures)
    {
        var list = figures.Where(x => x.Samples > 0).ToList();
        var samples = list.Sum(x => x.Samples);

        if (samples == 0)
            return new Combined(0, 0, 0, 0, 0);

        return new Combined(
            samples,
            list.Sum(x => x.Players),
            list.Sum(x => x.Sum),
            (int)Math.Round(list.Sum(x => (double)x.P50 * x.Samples) / samples),
            (int)Math.Round(list.Sum(x => (double)x.P95 * x.Samples) / samples)
        );
    }

    private static ClientPerformanceRow PerformanceRow(
        ClientPerformanceLogEntity x,
        IReadOnlyDictionary<int, string> players,
        IReadOnlyDictionary<int, string> rooms
    ) =>
        new(
            x.CreatedAt,
            x.PlayerId,
            players.GetValueOrDefault(x.PlayerId),
            x.RoomId,
            x.RoomId is { } room ? rooms.GetValueOrDefault(room) : null,
            x.BrowserFamily,
            x.OsFamily,
            x.DeviceType,
            x.UserAgent,
            ClientTelemetryRollups.Fps(x.AverageUpdateIntervalMs),
            x.MemoryKb / 1024,
            x.SlowUpdates,
            x.GarbageCollections
        );

    private static ClientLatencyRow LatencyRow(
        ClientLatencyReportEntity x,
        IReadOnlyDictionary<int, string> players,
        IReadOnlyDictionary<int, string> rooms
    ) =>
        new(
            x.CreatedAt,
            x.PlayerId,
            players.GetValueOrDefault(x.PlayerId),
            x.RoomId,
            x.RoomId is { } room ? rooms.GetValueOrDefault(room) : null,
            x.AverageMs,
            x.ValidAverageMs,
            x.PingCount
        );

    private static ClientLagRow LagRow(
        ClientLagWarningEntity x,
        IReadOnlyDictionary<int, string> players,
        IReadOnlyDictionary<int, string> rooms
    ) =>
        new(
            x.CreatedAt,
            x.PlayerId,
            players.GetValueOrDefault(x.PlayerId),
            x.RoomId,
            x.RoomId is { } room ? rooms.GetValueOrDefault(room) : null,
            x.WarningCount
        );

    private static async Task<Dictionary<int, string>> PlayerNamesAsync(
        TurboDbContext db,
        IEnumerable<int> ids,
        CancellationToken ct
    )
    {
        var wanted = ids.Distinct().ToList();

        return wanted.Count == 0
            ? []
            : await db
                .Players.AsNoTracking()
                .Where(x => wanted.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Name, ct)
                .ConfigureAwait(false);
    }

    private static async Task<Dictionary<int, string>> RoomNamesAsync(
        TurboDbContext db,
        IEnumerable<int> ids,
        CancellationToken ct
    )
    {
        var wanted = ids.Distinct().ToList();

        return wanted.Count == 0
            ? []
            : await db
                .Rooms.AsNoTracking()
                .Where(x => wanted.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Name, ct)
                .ConfigureAwait(false);
    }

    private readonly record struct Combined(int Samples, int Players, long Sum, int P50, int P95);
}
