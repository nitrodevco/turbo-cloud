using System.Collections;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Logging;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Navigator;
using Turbo.PacketHandlers.Room.Session;
using Turbo.Primitives;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Messages.Incoming.Room.Session;
using Turbo.Primitives.Messages.Outgoing.Nft;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Configuration;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A room id with no room behind it (deleted, or never there). Habbo answers the room info
/// request for one with nothing at all (official client capture 2026-10-18, 7 of 7), and a direct
/// entry with OpenConnection, the player's NFT chat styles, NoSuchFlat and CloseConnection (2 of
/// 2). Turbo woke a room grain for the id, whose activation threw on the missing row: the room
/// info request failed with "The specified room could not be found" logged four times over.
/// </summary>
public sealed class MissingRoomEntryTests
{
    private const int PLAYER = 101;
    private const int MISSING = 131;

    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public MissingRoomEntryTests()
    {
        // What a room grain does for an id with no row: its activation throws.
        _fakes.Handlers["GetSnapshotAsync"] = call =>
            call.Interface == typeof(IRoomGrain)
                ? throw new TurboException(TurboErrorCodeEnum.RoomNotFound)
                : Fakes.NotHandled;
        _fakes.Handlers["CheckEntryAccessAsync"] = _ =>
            throw new TurboException(TurboErrorCodeEnum.RoomNotFound);
        _fakes.Handlers["RoomExistsAsync"] = _ => Task.FromResult(false);
        _fakes.Handlers["GetPendingRoomAsync"] = _ =>
            Task.FromResult(new RoomPendingSnapshot { RoomId = -1, State = RoomEntryState.None });
        _fakes.Handlers["GetOwnedNftChatStyleIdsAsync"] = _ => Task.FromResult(new List<int>());
    }

    private MessageContext Ctx() => new(_fakes.Create<ISessionContext>(), PLAYER, -1);

    private IEnumerable<IComposer> Sent() =>
        _fakes
            .Log.Of("SendComposerAsync")
            .SelectMany(call => call.Args)
            .SelectMany(arg =>
                arg switch
                {
                    IComposer one => [one],
                    IEnumerable many => many.OfType<IComposer>(),
                    _ => [],
                }
            );

    private IEnumerable<FakeCall> RoomGrainCalls() => _fakes.Log.On<IRoomGrain>();

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task The_info_request_for_a_missing_room_gets_no_answer_and_wakes_no_room(
        bool enterRoom,
        bool roomForward
    )
    {
        await new GetGuestRoomMessageHandler(
            _fakes.Create<IRoomService>(),
            _fakes.Create<Orleans.IGrainFactory>(),
            _fakes.Create<INavigatorService>()
        ).HandleAsync(
            new GetGuestRoomMessage
            {
                RoomId = MISSING,
                EnterRoom = enterRoom,
                RoomForward = roomForward,
            },
            Ctx(),
            Ct
        );

        Sent().Should().BeEmpty();
        RoomGrainCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task A_direct_entry_to_a_missing_room_is_refused_as_missing_without_asking_the_room()
    {
        await new OpenFlatConnectionMessageHandler(
            _fakes.Create<IRoomService>(),
            _fakes.Create<Orleans.IGrainFactory>(),
            _fakes.Create<INavigatorService>()
        ).HandleAsync(new OpenFlatConnectionMessage { RoomId = MISSING }, Ctx(), Ct);

        _fakes.Log.Of("CheckRoomEntryAccessAsync").Should().BeEmpty();
        _fakes
            .Log.Of("OpenRoomForPlayerIdAsync")
            .Should()
            .ContainSingle()
            .Which.Args.OfType<RoomEntryAccessType>()
            .Should()
            .Equal(RoomEntryAccessType.NotFound);
    }

    [Fact]
    public async Task A_missing_room_is_opened_then_named_missing_and_closed_as_on_Habbo()
    {
        var service = (IRoomService)
            Activator.CreateInstance(
                typeof(RoomConfig).Assembly.GetType("Turbo.Rooms.RoomService")!,
                NullLogger<IRoomService>.Instance,
                Options.Create(new RoomConfig()),
                _fakes.Create<Orleans.IGrainFactory>(),
                TimeProvider.System
            )!;

        await service.OpenRoomForPlayerIdAsync(
            ActionContext.CreateForPlayer(PLAYER, -1),
            PLAYER,
            MISSING,
            RoomEntryAccessType.NotFound,
            Ct
        );

        Sent()
            .Select(x => x.GetType())
            .Should()
            .Equal(
                typeof(OpenConnectionMessageComposer),
                typeof(UserNftChatStylesMessageComposer),
                typeof(NoSuchFlatEventMessageComposer),
                typeof(CloseConnectionMessageComposer)
            );
        Sent().OfType<NoSuchFlatEventMessageComposer>().Single().RoomId.Value.Should().Be(MISSING);
        RoomGrainCalls().Should().BeEmpty();
    }
}
