using Turbo.Primitives.Availability;

namespace Turbo.Contracts.Hotel;

/// <summary>
/// Whether the hotel is open: the maintenance, shutdown or restart counting down or under way
/// (<c>:maintenance</c>, <c>:shutdown</c>, the panel's restart). A plugin's
/// <c>IEventHandler&lt;HotelAvailabilityChangedEvent&gt;</c> hears each change.
/// </summary>
public interface IHotelStatus
{
    /// <summary>
    /// The hotel now: its <see cref="HotelAvailabilitySnapshot.Phase"/>, when a countdown ends and
    /// its reason. <see cref="HotelAvailabilitySnapshot.BlocksLogin"/> while it is closed to
    /// players without <c>hotel.maintenance.bypass</c>.
    /// </summary>
    [HotelCall(HotelCallContext.Anywhere)]
    HotelAvailabilitySnapshot Current { get; }
}
