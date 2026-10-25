using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Hotel;
using Turbo.Contracts.Wired;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Testing;

/// <summary>A wired event a plugin fired in a room of a <see cref="FakeHotel"/>.</summary>
public sealed record FakeWiredFire(RoomId Room, IWiredEvent Event, PlayerId? Triggerer);

/// <summary>
/// The wired events of a <see cref="FakeHotel"/>: what the plugin fired, in which room and for
/// whom. Like the hotel's, an event reaches only a room someone is in, and the triggerer counts
/// only when they are in it. No stack runs: a plugin's boxes are tested in turbo-cloud's room
/// harness, or on a hotel.
/// </summary>
public sealed class FakeWired : IHotelWired
{
    private readonly FakeHotel _hotel;
    private readonly List<FakeWiredFire> _fired = [];

    internal FakeWired(FakeHotel hotel) => _hotel = hotel;

    public IReadOnlyList<FakeWiredFire> Fired => _fired;

    public Task<bool> FireAsync<TEvent>(
        RoomId room,
        TEvent evt,
        PlayerId? triggerer,
        CancellationToken ct
    )
        where TEvent : IWiredEvent
    {
        if (!_hotel.Rooms.IsPopulated(room))
            return Task.FromResult(false);

        _fired.Add(
            new FakeWiredFire(
                room,
                evt,
                triggerer is { } player && _hotel.Rooms.IsIn(room, player) ? player : null
            )
        );

        return Task.FromResult(true);
    }

    public Task<bool> FireForPlayerAsync<TEvent>(PlayerId player, TEvent evt, CancellationToken ct)
        where TEvent : IWiredEvent =>
        _hotel.Rooms.RoomOf(player) is { } room
            ? FireAsync(room, evt, player, ct)
            : Task.FromResult(false);

    /// <summary>The last <typeparamref name="TEvent"/> fired in <paramref name="room"/>.</summary>
    public TEvent AssertFired<TEvent>(RoomId room, Func<TEvent, bool>? match = null)
        where TEvent : IWiredEvent =>
        _fired
            .Where(x => x.Room == room)
            .Select(x => x.Event)
            .OfType<TEvent>()
            .LastOrDefault(x => match?.Invoke(x) ?? true)
        ?? throw new HotelAssertionException(
            $"Expected a {typeof(TEvent).Name} fired in room {room}; it was fired {FakeHotelText.List(_fired.Where(x => x.Room == room).Select(x => x.Event.GetType().Name))}."
        );

    public void AssertNothingFired()
    {
        if (_fired.Count > 0)
            throw new HotelAssertionException(
                $"Expected no wired event fired; {FakeHotelText.List(_fired.Select(x => $"{x.Event.GetType().Name} in room {x.Room}"))} was."
            );
    }
}
