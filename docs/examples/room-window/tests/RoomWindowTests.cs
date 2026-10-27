using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins.Messages;
using Turbo.Primitives.Policies;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Testing;
using Xunit;

namespace RoomWindow.Tests;

/// <summary>
/// The plugin's own tests: its handlers and its arrival policy against <see cref="FakeHotel"/>,
/// the windows' answers stubbed (<c>FakeRooms.FurniLogic</c>). The window's logic in a live room
/// is tested by the server (<c>Turbo.Tests/Plugins/RoomWindowExampleTests.cs</c>), which also
/// compiles this example as a hotel does.
/// </summary>
public sealed class RoomWindowTests
{
    private const int WINDOW = 10;
    private const int PARTNER = 20;

    private readonly FakeHotel _hotel = new();
    private readonly WindowDirectory _directory = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    /// The window in the lobby (its tile behind it (5, 1)) leads to the garden, where its other half
    /// stands on (2, 6): the garden is drawn with its (0, 0) on the lobby's (3, -5), so that half lands
    /// behind this one. The answer goes before the watch, so the client has the room drawn before
    /// its packets come.
    /// </summary>
    [Fact]
    public async Task A_look_draws_the_far_room_with_its_window_behind_this_one_then_watches_it()
    {
        var alice = _hotel.Players.Add("Alice", online: true);
        var lobby = _hotel.Rooms.Add("Lobby", alice);
        var garden = _hotel.Rooms.Add("Garden", alice);
        var session = new Session(alice, lobby);

        _directory.Add(lobby, WINDOW);
        _hotel.Rooms.FurniLogic = (_, item, message) =>
        {
            var ask = message.Read<WindowAsk>()!.Do;

            session.Asked.Add(ask);

            return (ask, item) switch
            {
                (WindowAsk.LOOK, WINDOW) => FurniLogicReply.Handled(
                    new LookAnswer(PARTNER, garden.Id.Value, 5, 1)
                ),
                (WindowAsk.WHERE, PARTNER) => FurniLogicReply.Handled(new RoomTilePoint(2, 6)),
                _ => FurniLogicReply.Handled(),
            };
        };

        await new LookHandler(_hotel, _directory).HandleAsync(new Look(), session, Ct);

        var views = Assert.IsType<Views>(Assert.Single(session.Replies));
        Assert.Equal([new WindowView(WINDOW, garden.Id.Value, 3, -5)], views.Windows);
        Assert.Equal(
            [
                (lobby.Id, WINDOW, WindowAsk.LOOK),
                (garden.Id, PARTNER, WindowAsk.WHERE),
                (lobby.Id, WINDOW, WindowAsk.WATCH),
            ],
            _hotel.Rooms.FurniMessages.Select(x =>
                (x.Room, x.ItemId, x.Message.Read<WindowAsk>()!.Do)
            )
        );
        Assert.Equal(["look", "where", "reply", "watch"], session.Asked);
        Assert.All(_hotel.Rooms.FurniMessages, x => Assert.Equal(alice.Id, x.Message.Sender));
    }

    /// <summary>A window whose other half is in an inventory shows nothing and watches nothing.</summary>
    [Fact]
    public async Task A_window_without_its_other_half_in_a_room_shows_nothing()
    {
        var alice = _hotel.Players.Add("Alice", online: true);
        var lobby = _hotel.Rooms.Add("Lobby", alice);
        var session = new Session(alice, lobby);

        _directory.Add(lobby, WINDOW);
        _hotel.Rooms.FurniLogic = (_, _, _) => FurniLogicReply.Handled();

        await new LookHandler(_hotel, _directory).HandleAsync(new Look(), session, Ct);

        Assert.Empty(Assert.IsType<Views>(Assert.Single(session.Replies)).Windows);
        Assert.Single(_hotel.Rooms.FurniMessages);
    }

    /// <summary>The player crossed through the window: the clicked tile goes to the window of their room.</summary>
    [Fact]
    public async Task A_cross_asks_the_window_in_the_players_room_with_the_clicked_tile()
    {
        var alice = _hotel.Players.Add("Alice", online: true);
        var lobby = _hotel.Rooms.Add("Lobby", alice);

        await new CrossHandler(_hotel).HandleAsync(
            new Cross(WINDOW, 3, 4),
            new Session(alice, lobby),
            Ct
        );

        var (room, item, message) = Assert.Single(_hotel.Rooms.FurniMessages);
        Assert.Equal((lobby.Id, WINDOW), (room, item));
        Assert.Equal(new WindowAsk(WindowAsk.CROSS, 3, 4), message.Read<WindowAsk>());
        Assert.Equal(RoomWindowLogic.LOGIC, message.Key);
    }

    /// <summary>
    /// A player a window sent appears on the tile they clicked, facing as they would at the door;
    /// anyone else (another plugin's extension, none) at the door.
    /// </summary>
    [Theory]
    [InlineData(RoomWindowLogic.EXTENSION, true)]
    [InlineData("other", false)]
    [InlineData(null, false)]
    public async Task A_player_from_a_window_appears_on_the_tile_they_clicked(
        string? key,
        bool onTheTile
    )
    {
        var door = RoomArrivalDecision.Door(0, 4, Rotation.East);
        var request = new RoomArrivalRequest(
            new PlayerId(1),
            new RoomId(2),
            RoomEntryMethodType.Default,
            new RoomId(1),
            key is null ? null : new RoomEntryExtension(key, """{"X":3,"Y":4}""")
        );

        var decision = await new WindowArrival().DecideAsync(request, door, Ct);

        Assert.Equal(onTheTile ? door.StartAt(3, 4, Rotation.East) : door, decision);
    }

    /// <summary>The session a message came from, as the server hands it to a handler.</summary>
    private sealed class Session(PlayerId player, RoomId room) : PluginMessageContext
    {
        public List<IPluginMessage> Replies { get; } = [];

        /// <summary>What the windows were asked and when the answer went, in order.</summary>
        public List<string> Asked { get; } = [];

        public override PlayerId PlayerId => player;

        public override RoomId RoomId => room;

        public override SessionKey SessionKey => default;

        public override Task ReplyAsync(IPluginMessage message, CancellationToken ct)
        {
            Replies.Add(message);
            Asked.Add("reply");

            return Task.CompletedTask;
        }
    }
}
