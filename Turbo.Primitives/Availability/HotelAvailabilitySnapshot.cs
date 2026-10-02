using System;

namespace Turbo.Primitives.Availability;

/// <summary>What the hotel is doing about being open, and when it next changes.</summary>
public sealed record HotelAvailabilitySnapshot(
    HotelAvailabilityPhase Phase,
    DateTime? AtUtc,
    string Reason
)
{
    public static HotelAvailabilitySnapshot Open { get; } =
        new(HotelAvailabilityPhase.Open, null, "");

    /// <summary>Whether a player without the bypass node is turned away at login.</summary>
    public bool BlocksLogin =>
        Phase is HotelAvailabilityPhase.Maintenance or HotelAvailabilityPhase.ShuttingDown;
}
