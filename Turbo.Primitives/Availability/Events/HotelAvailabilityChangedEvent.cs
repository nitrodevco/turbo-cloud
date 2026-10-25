using Turbo.Primitives.Events;

namespace Turbo.Primitives.Availability.Events;

/// <summary>
/// The hotel's availability changed: a maintenance, shutdown or restart was scheduled or called
/// off, or its countdown ran out (the hotel closed to players without
/// <c>hotel.maintenance.bypass</c>, or began to stop). <see cref="Current"/> is what
/// <c>IHotel.Status.Current</c> reads from now on.
/// </summary>
public sealed record HotelAvailabilityChangedEvent : DomainEvent
{
    public required HotelAvailabilitySnapshot Previous { get; init; }

    public required HotelAvailabilitySnapshot Current { get; init; }
}
