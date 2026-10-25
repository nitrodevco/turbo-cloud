using System;
using Turbo.Contracts.Hotel;
using Turbo.Primitives.Availability;

namespace Turbo.Testing;

/// <summary>The fake hotel's open or closed state: open until a test sets another.</summary>
public sealed class FakeStatus : IHotelStatus
{
    public HotelAvailabilitySnapshot Current { get; private set; } = HotelAvailabilitySnapshot.Open;

    /// <summary>The hotel as the test wants it: <c>new(HotelAvailabilityPhase.Maintenance, null, "")</c> closes it.</summary>
    public void Set(HotelAvailabilitySnapshot status)
    {
        ArgumentNullException.ThrowIfNull(status);

        Current = status;
    }
}
