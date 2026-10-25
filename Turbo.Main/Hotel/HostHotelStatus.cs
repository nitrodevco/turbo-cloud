using Turbo.Contracts.Hotel;
using Turbo.Primitives.Availability;

namespace Turbo.Main.Hotel;

/// <summary><see cref="IHotelStatus"/> over the host's maintenance and shutdown state.</summary>
internal sealed class HostHotelStatus(IHotelAvailability availability) : IHotelStatus
{
    public HotelAvailabilitySnapshot Current => availability.Current;
}
