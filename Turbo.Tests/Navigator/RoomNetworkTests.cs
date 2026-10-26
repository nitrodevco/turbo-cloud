using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Navigator;
using Turbo.Navigator.Configuration;
using Turbo.Players.Configuration;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Navigator;

/// <summary>
/// The reception's room hopper (<c>RoomHopperNetworkWidget</c>): its button opens a room session
/// on a network (<c>RoomSessionManager.gotoRoomNetwork</c>), whose open connection is
/// <c>RoomNetworkOpenConnection(networkId, homeRoomId)</c>, and waits for the server to pick the
/// room.
/// </summary>
public sealed class RoomNetworkTests
{
    private const int PLAYER = 1;
    private const int NETWORK = 5;

    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public RoomNetworkTests()
    {
        _fakes.Handlers["GetListingViewAsync"] = _ =>
            Task.FromResult(
                new RoomListingViewSnapshot
                {
                    ActiveRooms = [],
                    Epoch = Guid.Empty,
                    Sequence = 0,
                    ChangedKeys = [],
                    IsReset = false,
                }
            );
        // Room 12 of the network was deleted.
        _fakes.Handlers["GetRoomsByIdsAsync"] = call =>
            Task.FromResult(
                ((IReadOnlyCollection<RoomId>)call.Args[0]!)
                    .Where(x => x.Value is 10 or 11)
                    .Select(Room)
                    .ToList()
            );
    }

    [Fact]
    public async Task A_network_sends_a_player_to_one_of_its_rooms_that_exists()
    {
        var picked = new HashSet<RoomId>();

        for (var i = 0; i < 40; i++)
            picked.Add((await Service().GetRoomNetworkRoomAsync(NETWORK, 0, Ct))!.Value);

        picked.Should().BeEquivalentTo(new RoomId[] { 10, 11 });
    }

    [Fact]
    public async Task The_home_room_asked_for_is_kept_when_it_is_in_the_network()
    {
        (await Service().GetRoomNetworkRoomAsync(NETWORK, 11, Ct)).Should().Be((RoomId)11);
    }

    [Fact]
    public async Task A_network_with_no_rooms_sends_nowhere()
    {
        (await Service().GetRoomNetworkRoomAsync(6, 0, Ct)).Should().BeNull();
        (await Service().GetRoomNetworkRoomAsync(7, 0, Ct)).Should().BeNull();
    }

    [Fact]
    public async Task The_open_connection_forwards_the_player_as_arriving_by_room_network()
    {
        var harness = new PacketHarness();
        harness.Resolver.Overrides[typeof(INavigatorService)] = Service();

        var replies = await harness.SendAsync(
            PacketHarness.Incoming("RoomNetworkOpenConnectionMessageEvent"),
            PacketHarness.Payload(w => w.Int(NETWORK).Int(11)),
            playerId: PLAYER
        );

        replies.Should().BeEmpty();
        var forward = harness.Fakes.Log.Of("ForwardToRoomAsync").Should().ContainSingle().Subject;
        forward.Key.Should().Be((long)PLAYER);
        forward.Args[0].Should().Be((RoomId)11);
        ((RoomEntrySnapshot)forward.Args[1]!).Method.Should().Be(RoomEntryMethodType.RoomNetwork);
    }

    [Fact]
    public async Task An_unknown_network_closes_the_session_the_client_opened()
    {
        var harness = new PacketHarness();
        harness.Resolver.Overrides[typeof(INavigatorService)] = Service();

        var replies = await harness.SendAsync(
            PacketHarness.Incoming("RoomNetworkOpenConnectionMessageEvent"),
            PacketHarness.Payload(w => w.Int(99).Int(0)),
            playerId: PLAYER
        );

        replies
            .Select(x => x.Header)
            .Should()
            .Equal(PacketHarness.Outgoing("CloseConnectionMessageComposer"));
        harness.Fakes.Log.Of("ForwardToRoomAsync").Should().BeEmpty();
    }

    private NavigatorService Service() =>
        new(
            NullLogger<INavigatorService>.Instance,
            _fakes.Create<INavigatorProvider>(),
            _fakes.Create<IGrainFactory>(),
            Options.Create(
                new NavigatorConfig
                {
                    RoomNetworks = new()
                    {
                        [NETWORK] = [10, 11, 12, 10],
                        [6] = [12],
                        [7] = [],
                    },
                }
            ),
            Options.Create(new PlayerNavigatorConfig()),
            _fakes.Create<IPermissionRegistryProvider>(),
            _fakes.Create<IWordFilter>(),
            Turbo.Players.Policies.GamePolicies.None
        );

    private static RoomInfoSnapshot Room(RoomId roomId) =>
        ((RoomInfoSnapshot)RuntimeHelpers.GetUninitializedObject(typeof(RoomInfoSnapshot))) with
        {
            RoomId = roomId,
        };
}
