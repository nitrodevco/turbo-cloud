using System.Collections.Generic;
using System.Linq;
using Turbo.Contracts.Rooms;
using Turbo.Primitives.Rooms;

namespace Turbo.Testing;

/// <summary>A line said in a <see cref="TestRoom"/> by the room's code (a command, a furni), not by a player.</summary>
public sealed record TestRoomSpeech(string Speaker, string Text, RoomSpeech Speech);

/// <summary>
/// A room of a <see cref="TestHotel"/>: who is in it, what was said and sent in it, and its furni,
/// which run a plugin's furni logic (<see cref="Furni"/>). Passes as its <see cref="RoomId"/>.
/// </summary>
public sealed class TestRoom
{
    private readonly TestHotel _hotel;
    private readonly List<TestRoomSpeech> _said = [];
    private readonly List<string> _actions = [];
    private int _nextFurni = 1;

    internal TestRoom(TestHotel hotel, FakeRoom room)
    {
        _hotel = hotel;
        Fake = room;
    }

    public FakeRoom Fake { get; }

    internal TestHotel Hotel => _hotel;

    public RoomId Id => Fake.Id;

    public string Name => Fake.Name;

    /// <summary>The players standing in the room.</summary>
    public IReadOnlyList<TestPlayer> Players => [.. _hotel.AllPlayers.Where(p => p.Room == this)];

    /// <summary>The lines the room's code said, in order.</summary>
    public IReadOnlyList<TestRoomSpeech> Said => _said;

    /// <summary>The messages and composers plugins sent everyone in the room, in order.</summary>
    public IReadOnlyList<object> Sent => _hotel.Fake.Rooms.SentTo(Id);

    /// <summary>
    /// What a command or furni asked of the room that the kit only records (a kick, a mute, an
    /// effect, a stop), as <c>Method(arguments)</c>, in order.
    /// </summary>
    public IReadOnlyList<string> Actions => _actions;

    public static implicit operator RoomId(TestRoom room) => room.Id;

    /// <summary>
    /// A furni of <paramref name="logic"/> (a plugin's <c>[FurniLogic]</c> key) on the tile
    /// (<paramref name="x"/>, <paramref name="y"/>), attached as the room attaches its logic.
    /// </summary>
    public TestFurni Furni(string logic, int x = 0, int y = 0, int state = 0)
    {
        var plugin =
            _hotel.Plugins.Values.FirstOrDefault(p => p.HasFurniLogic(logic))
            ?? throw new HotelAssertionException(
                $"No live plugin has a [FurniLogic(\"{logic}\")] class: load it first."
            );

        return new TestFurni(
            _hotel,
            this,
            plugin.LogicFor(logic),
            _nextFurni++,
            logic,
            x,
            y,
            state
        );
    }

    internal void Say(string speaker, string text, RoomSpeech speech) =>
        _said.Add(new TestRoomSpeech(speaker, text, speech));

    internal void Act(string action) => _actions.Add(action);

    public override string ToString() => Fake.ToString();
}
