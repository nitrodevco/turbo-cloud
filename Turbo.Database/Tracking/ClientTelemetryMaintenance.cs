using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Database.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Entities.Tracking;

namespace Turbo.Database.Tracking;

/// <summary>
/// Keeps the client telemetry tables bounded: every
/// <see cref="ClientTelemetryConfig.MaintenanceIntervalMinutes"/> it rolls each finished day
/// (UTC) that has raw rows and no figures yet into <c>client_telemetry_daily</c>, then deletes raw
/// rows older than <see cref="ClientTelemetryConfig.RawRetentionDays"/> and figures older than
/// <see cref="ClientTelemetryConfig.RollupRetentionDays"/>. A day is rolled up before its raw rows
/// can expire, so nothing is deleted that was not counted.
/// </summary>
public sealed class ClientTelemetryMaintenance(
    IOptionsMonitor<ClientTelemetryConfig> config,
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    TimeProvider timeProvider,
    ILogger<ClientTelemetryMaintenance> logger
) : BackgroundService
{
    /// <summary>Rolls up the finished days and deletes what has expired; returns the days rolled up.</summary>
    public async Task<int> RunAsync(CancellationToken ct)
    {
        var current = config.CurrentValue;
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var rolled = 0;

        foreach (var day in await DaysToRollUpAsync(db, today, ct).ConfigureAwait(false))
        {
            var figures = await ClientTelemetryRollups
                .BuildDayAsync(db, day, ct)
                .ConfigureAwait(false);

            figures.Add(
                new ClientTelemetryDailyEntity
                {
                    Day = day,
                    Kind = ClientTelemetryKinds.DAY,
                    Key1 = "",
                    Key2 = "",
                    Key3 = "",
                    Samples = figures.Count,
                    Players = 0,
                    Sum = 0,
                    P50 = 0,
                    P95 = 0,
                    Max = 0,
                }
            );
            db.ClientTelemetryDaily.AddRange(figures);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            rolled++;
        }

        if (current.RawRetentionDays > 0)
        {
            var before = today
                .AddDays(-current.RawRetentionDays)
                .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

            await db
                .ClientPerformanceLogs.Where(x => x.CreatedAt < before)
                .ExecuteDeleteAsync(ct)
                .ConfigureAwait(false);
            await db
                .ClientLatencyReports.Where(x => x.CreatedAt < before)
                .ExecuteDeleteAsync(ct)
                .ConfigureAwait(false);
            await db
                .ClientLagWarnings.Where(x => x.CreatedAt < before)
                .ExecuteDeleteAsync(ct)
                .ConfigureAwait(false);
            await db
                .ClientEventLogs.Where(x => x.CreatedAt < before)
                .ExecuteDeleteAsync(ct)
                .ConfigureAwait(false);
            await db
                .ClientLoginSteps.Where(x => x.CreatedAt < before)
                .ExecuteDeleteAsync(ct)
                .ConfigureAwait(false);
        }

        if (current.RollupRetentionDays > 0)
        {
            var before = today.AddDays(-current.RollupRetentionDays);

            await db
                .ClientTelemetryDaily.Where(x => x.Day < before)
                .ExecuteDeleteAsync(ct)
                .ConfigureAwait(false);
        }

        return rolled;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var rolled = await RunAsync(stoppingToken).ConfigureAwait(false);

                if (rolled > 0)
                    logger.LogInformation("Rolled up {Days} days of client telemetry", rolled);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Client telemetry maintenance failed; trying again later");
            }

            try
            {
                await Task.Delay(
                        TimeSpan.FromMinutes(
                            Math.Max(1, config.CurrentValue.MaintenanceIntervalMinutes)
                        ),
                        timeProvider,
                        stoppingToken
                    )
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    /// <summary>The days before today with raw rows and no figures, oldest first.</summary>
    private static async Task<List<DateOnly>> DaysToRollUpAsync(
        TurboDbContext db,
        DateOnly today,
        CancellationToken ct
    )
    {
        var start = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var oldest = new[]
        {
            await db
                .ClientPerformanceLogs.Where(x => x.CreatedAt < start)
                .MinAsync(x => (DateTime?)x.CreatedAt, ct)
                .ConfigureAwait(false),
            await db
                .ClientLatencyReports.Where(x => x.CreatedAt < start)
                .MinAsync(x => (DateTime?)x.CreatedAt, ct)
                .ConfigureAwait(false),
            await db
                .ClientLagWarnings.Where(x => x.CreatedAt < start)
                .MinAsync(x => (DateTime?)x.CreatedAt, ct)
                .ConfigureAwait(false),
            await db
                .ClientEventLogs.Where(x => x.CreatedAt < start)
                .MinAsync(x => (DateTime?)x.CreatedAt, ct)
                .ConfigureAwait(false),
            await db
                .ClientLoginSteps.Where(x => x.CreatedAt < start)
                .MinAsync(x => (DateTime?)x.CreatedAt, ct)
                .ConfigureAwait(false),
        }
            .Where(x => x is not null)
            .Select(x => x!.Value)
            .DefaultIfEmpty(start)
            .Min();

        var first = DateOnly.FromDateTime(oldest);
        var done = await db
            .ClientTelemetryDaily.AsNoTracking()
            .Where(x => x.Kind == ClientTelemetryKinds.DAY && x.Day >= first)
            .Select(x => x.Day)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var days = new List<DateOnly>();

        for (var day = first; day < today; day = day.AddDays(1))
        {
            if (!done.Contains(day))
                days.Add(day);
        }

        return days;
    }
}
