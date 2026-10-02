using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Availability;

/// <summary>
/// Whether the hotel is open, and the maintenance or shutdown that is counting down. It lives in
/// the process: a restart opens the hotel again, which is what a restart is for.
/// </summary>
public interface IHotelAvailability
{
    HotelAvailabilitySnapshot Current { get; }

    /// <summary>
    /// Whether the player may log in now: always when the hotel is open, in maintenance only
    /// with <c>hotel.maintenance.bypass</c>, and never once it is shutting down.
    /// </summary>
    Task<bool> AdmitsAsync(PlayerId playerId, CancellationToken ct);

    /// <summary>
    /// Counts down to maintenance. Replaces a maintenance already scheduled; false when a
    /// shutdown is counting down, which wins.
    /// </summary>
    bool ScheduleMaintenance(TimeSpan delay, string reason);

    /// <summary>Counts down to closing the hotel down. Replaces anything scheduled.</summary>
    void ScheduleShutdown(TimeSpan delay, string reason);

    /// <summary>
    /// Calls off a countdown, or ends a maintenance; false when the hotel was open, and when it is
    /// already shutting down, which nothing calls off.
    /// </summary>
    bool Cancel();
}
