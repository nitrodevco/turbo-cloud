using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Database.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Entities.Tracking;
using Turbo.Primitives.Messages.Incoming.Tracking;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Tracking;

namespace Turbo.Database.Tracking;

/// <summary>
/// The client telemetry recorder (<see cref="IClientTelemetry"/>): a report becomes a row on a
/// bounded queue at once, and this hosted service writes the queue every
/// <see cref="ClientTelemetryConfig.FlushIntervalMs"/> in batches of
/// <see cref="ClientTelemetryConfig.BatchSize"/>. A report over the player's per-minute limit, or
/// one that finds the queue full, is dropped and counted; a batch the database refuses is logged
/// and dropped. Per session it keeps the device of the last performance report, for the latency
/// reports that do not say it, and when the socket opened, for the login steps; both are let go
/// once the session is quiet.
/// </summary>
public sealed class ClientTelemetryRecorder(
    IOptionsMonitor<ClientTelemetryConfig> config,
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    TimeProvider timeProvider,
    ILogger<ClientTelemetryRecorder> logger
) : BackgroundService, IClientTelemetry
{
    /// <summary>A session not heard from for this long is forgotten.</summary>
    private static readonly TimeSpan SESSION_IDLE = TimeSpan.FromHours(1);

    private readonly Channel<object> _queue = Channel.CreateBounded<object>(
        new BoundedChannelOptions(Math.Max(1, config.CurrentValue.QueueCapacity))
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        }
    );
    private readonly ConcurrentDictionary<int, Allowance> _reportAllowances = new();
    private readonly ConcurrentDictionary<int, Allowance> _eventAllowances = new();
    private readonly ConcurrentDictionary<string, SessionState> _sessions = new();
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private long _droppedFull;
    private long _droppedLimited;

    public void RecordPerformance(
        SessionKey session,
        PlayerId playerId,
        RoomId roomId,
        PerformanceLogMessage report
    )
    {
        if (!Accepts(playerId, _reportAllowances, config.CurrentValue.MaxReportsPerPlayerPerMinute))
            return;

        var now = Now();
        var userAgent = Clip(report.UserAgent, 512);
        var device = ClientUserAgents.Classify(userAgent);

        if (SessionOf(session, now) is { } state)
            state.Device = device;

        Enqueue(
            new ClientPerformanceLogEntity
            {
                CreatedAt = now,
                PlayerId = playerId,
                RoomId = RoomOf(roomId),
                ElapsedSeconds = report.ElapsedTime,
                UserAgent = userAgent,
                FlashVersion = Clip(report.FlashVersion, 64),
                Os = Clip(report.OS, 64),
                Browser = Clip(report.Browser, 64),
                BrowserFamily = device.Browser,
                OsFamily = device.Os,
                DeviceType = device.Device,
                IsDebugger = report.IsDebugger,
                MemoryKb = Math.Max(0, report.MemoryUsage),
                GarbageCollections = Math.Max(0, report.GarbageCollections),
                AverageUpdateIntervalMs = Math.Max(0, report.AverageUpdateInterval),
                SlowUpdates = Math.Max(0, report.SlowUpdates),
            }
        );
    }

    public void RecordLatency(
        SessionKey session,
        PlayerId playerId,
        RoomId roomId,
        LatencyPingReportMessage report
    )
    {
        if (!Accepts(playerId, _reportAllowances, config.CurrentValue.MaxReportsPerPlayerPerMinute))
            return;

        var now = Now();
        var device =
            SessionOf(session, now)?.Device
            ?? (ClientUserAgents.UNKNOWN, ClientUserAgents.UNKNOWN, ClientUserAgents.UNKNOWN);

        Enqueue(
            new ClientLatencyReportEntity
            {
                CreatedAt = now,
                PlayerId = playerId,
                RoomId = RoomOf(roomId),
                AverageMs = Math.Max(0, report.AverageLatency),
                ValidAverageMs = Math.Max(0, report.ValidPingAverage),
                PingCount = Math.Max(0, report.NumPings),
                BrowserFamily = device.Browser,
                OsFamily = device.Os,
                DeviceType = device.Device,
            }
        );
    }

    public void RecordLagWarnings(PlayerId playerId, RoomId roomId, LagWarningReportMessage report)
    {
        if (report.WarningCount <= 0)
            return;

        if (!Accepts(playerId, _reportAllowances, config.CurrentValue.MaxReportsPerPlayerPerMinute))
            return;

        Enqueue(
            new ClientLagWarningEntity
            {
                CreatedAt = Now(),
                PlayerId = playerId,
                RoomId = RoomOf(roomId),
                WarningCount = report.WarningCount,
            }
        );
    }

    public void RecordEvent(PlayerId playerId, RoomId roomId, EventLogMessage report)
    {
        if (!Accepts(playerId, _eventAllowances, config.CurrentValue.MaxEventsPerPlayerPerMinute))
            return;

        Enqueue(
            new ClientEventLogEntity
            {
                CreatedAt = Now(),
                PlayerId = playerId,
                RoomId = RoomOf(roomId),
                Category = Clip(report.Category, 64),
                Type = Clip(report.Type, 128),
                Action = Clip(report.Action, 128),
                ExtraString = Clip(report.ExtraString, 255),
                ExtraInt = report.ExtraInt,
            }
        );
    }

    public void RecordLoginStep(SessionKey session, PlayerId playerId, string step)
    {
        var current = config.CurrentValue;

        if (!current.Enabled || string.IsNullOrEmpty(session))
            return;

        var now = Now();
        var state = step == ClientLoginSteps.SOCKET_OK ? SessionOf(session, now) : Known(session);

        if (state is null)
            return;

        var elapsed = now - state.ConnectedAt;

        if (elapsed > TimeSpan.FromMinutes(current.LoginTrackingMinutes))
            return;

        lock (state)
        {
            if (!state.Steps.Add(step))
                return;
        }

        state.LastSeen = now;
        Enqueue(
            new ClientLoginStepEntity
            {
                CreatedAt = now,
                Session = Clip(session, 64),
                PlayerId = playerId > 0 ? playerId : null,
                Step = Clip(step, 64),
                MsSinceConnect = (int)Math.Min(int.MaxValue, elapsed.TotalMilliseconds),
            }
        );
    }

    /// <summary>
    /// Writes everything queued now, in batches, and lets go of quiet sessions and spent
    /// allowances. The service calls it every <see cref="ClientTelemetryConfig.FlushIntervalMs"/>
    /// and once more when the server stops.
    /// </summary>
    public async Task FlushAsync(CancellationToken ct)
    {
        await _flushLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var batchSize = Math.Max(1, config.CurrentValue.BatchSize);
            var batch = new List<object>(batchSize);

            while (_queue.Reader.TryRead(out var row))
            {
                batch.Add(row);

                if (batch.Count < batchSize)
                    continue;

                await WriteAsync(batch, ct).ConfigureAwait(false);
                batch.Clear();
            }

            if (batch.Count > 0)
                await WriteAsync(batch, ct).ConfigureAwait(false);

            Forget(Now());
            ReportDropped();
        }
        finally
        {
            _flushLock.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(
                        TimeSpan.FromMilliseconds(
                            Math.Max(100, config.CurrentValue.FlushIntervalMs)
                        ),
                        timeProvider,
                        stoppingToken
                    )
                    .ConfigureAwait(false);
                await FlushAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        await FlushAsync(CancellationToken.None).ConfigureAwait(false);
    }

    public override void Dispose()
    {
        _flushLock.Dispose();
        base.Dispose();
    }

    private async Task WriteAsync(List<object> batch, CancellationToken ct)
    {
        try
        {
            var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var dbScope = db.ConfigureAwait(false);

            db.AddRange(batch);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Dropped {Count} client telemetry rows: the write failed",
                batch.Count
            );
        }
    }

    private bool Accepts(
        PlayerId playerId,
        ConcurrentDictionary<int, Allowance> allowances,
        int max
    )
    {
        if (!config.CurrentValue.Enabled || playerId <= 0)
            return false;

        if (max <= 0)
            return true;

        var minute = Now().Ticks / TimeSpan.TicksPerMinute;
        var allowance = allowances.GetOrAdd(playerId, _ => new Allowance());

        lock (allowance)
        {
            if (allowance.Minute != minute)
            {
                allowance.Minute = minute;
                allowance.Used = 0;
            }

            if (allowance.Used >= max)
            {
                Interlocked.Increment(ref _droppedLimited);

                return false;
            }

            allowance.Used++;
        }

        return true;
    }

    private void Enqueue(object row)
    {
        if (!_queue.Writer.TryWrite(row))
            Interlocked.Increment(ref _droppedFull);
    }

    private SessionState? SessionOf(SessionKey session, DateTime now)
    {
        if (string.IsNullOrEmpty(session))
            return null;

        var state = _sessions.GetOrAdd(session, _ => new SessionState(now));
        state.LastSeen = now;

        return state;
    }

    private SessionState? Known(SessionKey session) =>
        !string.IsNullOrEmpty(session) && _sessions.TryGetValue(session, out var state)
            ? state
            : null;

    private void Forget(DateTime now)
    {
        var minute = now.Ticks / TimeSpan.TicksPerMinute;

        foreach (var (key, state) in _sessions)
        {
            if (now - state.LastSeen > SESSION_IDLE)
                _sessions.TryRemove(key, out _);
        }

        foreach (var allowances in new[] { _reportAllowances, _eventAllowances })
        {
            foreach (var (key, allowance) in allowances)
            {
                if (allowance.Minute < minute)
                    allowances.TryRemove(key, out _);
            }
        }
    }

    private void ReportDropped()
    {
        var full = Interlocked.Exchange(ref _droppedFull, 0);
        var limited = Interlocked.Exchange(ref _droppedLimited, 0);

        if (full > 0)
            logger.LogWarning("Dropped {Count} client telemetry rows: the queue was full", full);

        if (limited > 0)
            logger.LogDebug(
                "Dropped {Count} client telemetry reports over a player's per-minute limit",
                limited
            );
    }

    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;

    private static int? RoomOf(RoomId roomId) => roomId > 0 ? roomId.Value : null;

    private static string Clip(string? value, int length) =>
        value is null ? string.Empty
        : value.Length <= length ? value
        : value[..length];

    private sealed class Allowance
    {
        public long Minute;
        public int Used;
    }

    private sealed class SessionState(DateTime connectedAt)
    {
        public DateTime ConnectedAt { get; } = connectedAt;
        public DateTime LastSeen { get; set; } = connectedAt;
        public (string Browser, string Os, string Device)? Device { get; set; }
        public HashSet<string> Steps { get; } = [];
    }
}
