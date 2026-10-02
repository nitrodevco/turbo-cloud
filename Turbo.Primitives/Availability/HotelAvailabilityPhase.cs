namespace Turbo.Primitives.Availability;

public enum HotelAvailabilityPhase
{
    Open,

    /// <summary>A maintenance is counting down; the hotel is still open.</summary>
    MaintenanceScheduled,

    /// <summary>Only players holding <c>hotel.maintenance.bypass</c> may be in.</summary>
    Maintenance,

    /// <summary>A shutdown is counting down.</summary>
    ShutdownScheduled,

    /// <summary>The countdown ran out: everyone was sent home and the host is stopping.</summary>
    ShuttingDown,
}
