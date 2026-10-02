using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Operations.Configuration;
using Turbo.Primitives.Availability;
using Turbo.Primitives.Messages.Outgoing.Availability;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Texts;

namespace Turbo.Operations;

/// <summary>
/// The hotel's maintenance and shutdown. A command starts a countdown; this reminds the hotel as
/// it runs down, and at zero either closes the hotel to everyone without the bypass node (a
/// maintenance) or sends everybody home and stops the host (a shutdown). It is process state: a
/// restart opens the hotel again, which is what a restart is for.
/// </summary>
public sealed class HotelAvailabilityService(
    IOptions<OperationsConfig> config,
    TimeProvider timeProvider,
    ISessionGateway sessionGateway,
    IGrainFactory grainFactory,
    IHotelTextProvider textProvider,
    IHostApplicationLifetime lifetime,
    ILogger<IHotelAvailability> logger
) : BackgroundService, IHotelAvailability
{
    private readonly OperationsConfig _config = config.Value;
    private readonly Lock _gate = new();
    private readonly HashSet<int> _announced = [];

    private HotelAvailabilitySnapshot _current = HotelAvailabilitySnapshot.Open;
    private bool _announcePending;

    public HotelAvailabilitySnapshot Current
    {
        get
        {
            lock (_gate)
                return _current;
        }
    }

    public async Task<bool> AdmitsAsync(PlayerId playerId, CancellationToken ct)
    {
        var current = Current;

        if (!current.BlocksLogin)
            return true;

        // Nobody is let in to a hotel that is stopping, staff included: they would be cut off.
        return current.Phase != HotelAvailabilityPhase.ShuttingDown
            && await grainFactory.HasPermissionAsync(
                playerId,
                PermissionNodes.Hotel.MAINTENANCE_BYPASS,
                ct
            );
    }

    public bool ScheduleMaintenance(TimeSpan delay, string reason)
    {
        lock (_gate)
        {
            if (
                _current.Phase
                is HotelAvailabilityPhase.ShutdownScheduled
                    or HotelAvailabilityPhase.ShuttingDown
            )
                return false;

            Schedule(HotelAvailabilityPhase.MaintenanceScheduled, delay, reason);
        }

        return true;
    }

    public void ScheduleShutdown(TimeSpan delay, string reason)
    {
        lock (_gate)
        {
            // Once it is stopping there is nothing to schedule.
            if (_current.Phase != HotelAvailabilityPhase.ShuttingDown)
                Schedule(HotelAvailabilityPhase.ShutdownScheduled, delay, reason);
        }
    }

    public bool Cancel()
    {
        lock (_gate)
        {
            if (
                _current.Phase is HotelAvailabilityPhase.Open or HotelAvailabilityPhase.ShuttingDown
            )
                return false;

            _current = HotelAvailabilitySnapshot.Open;
            _announcePending = false;
            _announced.Clear();
        }

        return true;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_config.CountdownTickMs));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await TickAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping, which is what a shutdown countdown ends in too.
        }
    }

    /// <summary>One look at the clock: remind the hotel, or act when the countdown has run out.</summary>
    public async Task TickAsync(CancellationToken ct)
    {
        HotelAvailabilitySnapshot scheduled;
        TimeSpan remaining;
        var remind = false;

        lock (_gate)
        {
            scheduled = _current;

            if (
                scheduled.Phase
                    is not (
                        HotelAvailabilityPhase.MaintenanceScheduled
                        or HotelAvailabilityPhase.ShutdownScheduled
                    )
                || scheduled.AtUtc is not { } at
            )
                return;

            remaining = at - timeProvider.GetUtcNow().UtcDateTime;

            if (remaining > TimeSpan.Zero)
            {
                remind = _announcePending;
                _announcePending = false;

                // A reminder the countdown has already passed is not made up for.
                foreach (var minutes in _config.CountdownReminderMinutes)
                    if (remaining <= TimeSpan.FromMinutes(minutes) && _announced.Add(minutes))
                        remind = true;
            }
        }

        if (remaining <= TimeSpan.Zero)
            await FireAsync(scheduled, ct);
        else if (remind)
            await AnnounceAsync(scheduled, (int)Math.Ceiling(remaining.TotalMinutes), ct);
    }

    private void Schedule(HotelAvailabilityPhase phase, TimeSpan delay, string reason)
    {
        var at = timeProvider.GetUtcNow().UtcDateTime + delay;

        _current = new HotelAvailabilitySnapshot(phase, at, reason);
        _announced.Clear();
        _announcePending = true;

        // A reminder this countdown is already inside has been made by the announcement itself.
        foreach (var minutes in _config.CountdownReminderMinutes)
            if (delay <= TimeSpan.FromMinutes(minutes))
                _announced.Add(minutes);
    }

    private async Task AnnounceAsync(
        HotelAvailabilitySnapshot current,
        int minutes,
        CancellationToken ct
    )
    {
        try
        {
            IComposer composer =
                current.Phase == HotelAvailabilityPhase.ShutdownScheduled
                    ? new InfoHotelClosingMessageComposer { MinutesUntilClosing = minutes }
                    : new MaintenanceStatusMessageComposer
                    {
                        IsInMaintenance = false,
                        MinutesUntilMaintenance = minutes,
                    };

            var online = sessionGateway.GetOnlinePlayerIds();

            await grainFactory.SendComposerToPlayersAsync(online, composer, ct);

            if (current.Reason.Length > 0)
                await grainFactory.SendComposerToPlayersAsync(
                    online,
                    new HabboBroadcastMessageComposer { Message = current.Reason },
                    ct
                );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Failed to announce the {Phase} countdown, {Minutes} minutes left",
                current.Phase,
                minutes
            );
        }
    }

    private async Task FireAsync(HotelAvailabilitySnapshot scheduled, CancellationToken ct)
    {
        lock (_gate)
        {
            // Called off, or replaced by another countdown, while this tick was deciding.
            if (_current != scheduled)
                return;

            // Neither a countdown nor open any more, so the next tick leaves it alone: the host
            // takes a moment to stop.
            _current = new HotelAvailabilitySnapshot(
                scheduled.Phase == HotelAvailabilityPhase.ShutdownScheduled
                    ? HotelAvailabilityPhase.ShuttingDown
                    : HotelAvailabilityPhase.Maintenance,
                null,
                scheduled.Reason
            );
        }

        if (scheduled.Phase == HotelAvailabilityPhase.ShutdownScheduled)
        {
            logger.LogWarning("The shutdown countdown ran out; closing the hotel and stopping");

            await DisconnectAsync(
                sessionGateway.GetOnlinePlayerIds(),
                AvailabilityMessages.ShuttingDown(textProvider),
                ct
            );

            lifetime.StopApplication();

            return;
        }

        logger.LogWarning("The maintenance countdown ran out; the hotel is closed to players");

        var staying = new List<PlayerId>();
        var leaving = new List<PlayerId>();

        foreach (var playerId in sessionGateway.GetOnlinePlayerIds())
            (
                await grainFactory.HasPermissionAsync(
                    playerId,
                    PermissionNodes.Hotel.MAINTENANCE_BYPASS,
                    ct
                )
                    ? staying
                    : leaving
            ).Add(playerId);

        await DisconnectAsync(leaving, AvailabilityMessages.MaintenanceStarted(textProvider), ct);
    }

    private async Task DisconnectAsync(
        IEnumerable<PlayerId> players,
        string farewell,
        CancellationToken ct
    )
    {
        foreach (var playerId in players)
        {
            try
            {
                await sessionGateway.DisconnectPlayerAsync(
                    playerId,
                    new HabboBroadcastMessageComposer { Message = farewell },
                    ct
                );
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to disconnect player {PlayerId}", playerId);
            }
        }
    }
}
