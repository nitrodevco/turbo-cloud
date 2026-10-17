using System;

namespace Turbo.Primitives.Availability;

/// <summary>
/// What the hotel is doing about being open, and when it next changes. A shutdown with
/// <see cref="Restart"/> is a restart: the process ends with the restart exit code, for the host
/// to start it again.
/// </summary>
public sealed record HotelAvailabilitySnapshot(
    HotelAvailabilityPhase Phase,
    DateTime? AtUtc,
    string Reason,
    bool Restart = false
)
{
    public static HotelAvailabilitySnapshot Open { get; } =
        new(HotelAvailabilityPhase.Open, null, "");

    /// <summary>Whether a player without the bypass node is turned away at login.</summary>
    public bool BlocksLogin =>
        Phase is HotelAvailabilityPhase.Maintenance or HotelAvailabilityPhase.ShuttingDown;
}
