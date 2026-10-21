using FluentAssertions;
using Orleans;
using Orleans.Streams;
using Turbo.Database.Entities.Guilds;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Guilds.Grains;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Users;
using Turbo.Players.Configuration;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Grains;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Grains.Guilds;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Guilds;

/// <summary>
/// What a player is sent after their worn group badge changes follows the room's badge messages,
/// as in Habbo. Joining open group 368818 from its homeroom, twice: HabboGroupBadges and
/// FavoriteMembershipUpdate, then ExtendedProfileChanged ~50 ms later (official client capture
/// 2026-10-13 20:55 and 21:07). Leaving group 402778 while wearing it: FavoriteMembershipUpdate,
/// then GuildMembershipRejected, ExtendedProfileChanged and GroupDetailsChanged (14:23). The room
/// sends its part through the room stream and is told, not awaited (it reads the player's groups
/// on entry, so awaiting it could deadlock), so Turbo's own replies overtook it: join sent
/// ExtendedProfileChanged first, leave sent FavoriteMembershipUpdate last. Dropping and picking
/// group 402778 as favourite: FavoriteMembershipUpdate (after HabboGroupBadges when picked), then
/// ExtendedProfileChanged (14:21); Turbo sent the profile change on the session, ahead of both.
///
/// The real presence, player group grain and group grain run here; only the room is played by
/// the test, which delivers its publishes after the replies were sent, the way the stream does.
/// </summary>
public sealed class GroupReplyOrderTests : IDisposable
{
    private const int GROUP = 10;
    private const int ROOM = 9;
    private const int PLAYER = SessionHarness.PlayerId;
    private const int OWNER = 2;

    private readonly SessionHarness _hotel = new();
    private readonly SqliteDb _db = new();
    private readonly List<FakeCall> _roomTold = [];
    private SessionKey _key;
    private ISessionContext _session = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public GroupReplyOrderTests()
    {
        foreach (var (id, name) in new[] { (PLAYER, "player"), (OWNER, "owner") })
            _db.Insert(
                new PlayerEntity
                {
                    Id = id,
                    Name = name,
                    Figure = "hd-180-1",
                    Gender = AvatarGenderType.Male,
                    PlayerStatus = PlayerStatusType.Offline,
                }
            );

        _db.Insert(
            new RoomModelEntity
            {
                Id = 1,
                Name = "model",
                Model = "00\r00",
                DoorX = 0,
                DoorY = 0,
                DoorRotation = Rotation.North,
                Enabled = true,
                Custom = false,
            }
        );
        _db.Insert(
            new RoomEntity
            {
                Id = ROOM,
                Name = "homeroom",
                PlayerEntityId = OWNER,
                RoomModelEntityId = 1,
                DoorMode = RoomDoorModeType.Open,
                UsersNow = 0,
                PlayersMax = 25,
                WallHeight = -1,
                HideWalls = false,
                ThicknessWall = RoomThicknessType.Normal,
                ThicknessFloor = RoomThicknessType.Normal,
                AllowBlocking = false,
                AllowPets = true,
                AllowPetsEat = true,
                TradeType = RoomTradeModeType.Disabled,
                MuteType = ModSettingType.Owner,
                KickType = ModSettingType.Owner,
                BanType = ModSettingType.Owner,
                ChatFloodType = ChatFloodSensitivityType.Minimal,
                PlayerEntity = null!,
                RoomModelEntity = null!,
            }
        );
        _db.Insert(
            new GuildEntity
            {
                Id = GROUP,
                Name = "Group",
                Description = "",
                BadgeCode = "b05114s19134",
                PrimaryColorId = 1,
                SecondaryColorId = 1,
                GuildType = GuildType.Regular,
                RightsLevel = GuildRightsLevel.Owner,
                PlayerEntityId = OWNER,
                RoomEntityId = ROOM,
            }
        );
        _db.Insert(
            new GuildMemberEntity
            {
                Id = 1,
                GuildEntityId = GROUP,
                PlayerEntityId = OWNER,
                Rank = GuildMemberRank.Owner,
                IsFavourite = true,
            }
        );

        _hotel.Fakes.Handlers["GetEditorDataAsync"] = _ =>
            Task.FromResult(
                new GuildEditorDataSnapshot
                {
                    BaseParts = [],
                    SymbolParts = [],
                    BadgeColors = [],
                    PrimaryColors = [],
                    SecondaryColors = [],
                }
            );

        // The room, told: it publishes later, when the test says so.
        _hotel.Fakes.Handlers["SetPlayerFavouriteGuildAsync"] = call =>
        {
            lock (_roomTold)
                _roomTold.Add(call);

            return Task.CompletedTask;
        };
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public Task Joining_says_the_profile_changed_after_the_rooms_badge_messages() =>
        SessionHarness.InTurns(async () =>
        {
            var player = await OnlineInRoomAsync(memberWearing: false);

            // What JoinHabboGroupMessageHandler does once the join succeeded.
            await player.WearIfNoneAsync(GuildId.Parse(GROUP), Ct);
            await SendProfileChangedAsync();
            await SentAsync(0);

            Received().Should().BeEmpty("the profile change waits for the room's badge messages");

            await RoomPublishesAsync();
            await SentAsync(2);

            Received()
                .Should()
                .Equal(
                    nameof(FavoriteMembershipUpdateMessageComposer),
                    nameof(ExtendedProfileChangedMessageComposer)
                );
        });

    [Fact]
    public Task Picking_a_favourite_says_the_profile_changed_after_the_rooms_badge_messages() =>
        SessionHarness.InTurns(async () =>
        {
            await OnlineInRoomAsync(memberWearing: false);

            await new SelectFavouriteHabboGroupMessageHandler(
                _hotel.Fakes.Create<IGrainFactory>()
            ).HandleAsync(new SelectFavouriteHabboGroupMessage { GuildId = GROUP }, Ctx(), Ct);
            await SentAsync(0);

            Received().Should().BeEmpty("the profile change waits for the room's badge messages");

            await RoomPublishesAsync();
            await SentAsync(2);

            Received()
                .Should()
                .Equal(
                    nameof(FavoriteMembershipUpdateMessageComposer),
                    nameof(ExtendedProfileChangedMessageComposer)
                );
        });

    [Fact]
    public Task Dropping_the_favourite_says_the_profile_changed_after_the_rooms_badge_message() =>
        SessionHarness.InTurns(async () =>
        {
            await OnlineInRoomAsync(memberWearing: true);

            await new DeselectFavouriteHabboGroupMessageHandler(
                _hotel.Fakes.Create<IGrainFactory>()
            ).HandleAsync(new DeselectFavouriteHabboGroupMessage { GuildId = GROUP }, Ctx(), Ct);
            await SentAsync(0);

            Received().Should().BeEmpty("the profile change waits for the room's badge message");

            await RoomPublishesAsync();
            await SentAsync(2);

            Received()
                .Should()
                .Equal(
                    nameof(FavoriteMembershipUpdateMessageComposer),
                    nameof(ExtendedProfileChangedMessageComposer)
                );
        });

    [Fact]
    public Task Leaving_the_worn_group_sends_the_badge_change_before_the_leave_replies() =>
        SessionHarness.InTurns(async () =>
        {
            await OnlineInRoomAsync(memberWearing: true);
            var group = await GroupAsync();

            await group.KickAsync(PLAYER, PLAYER, false, Ct);
            await RoomPublishesAsync();
            await SentAsync(4);

            Received()
                .Should()
                .Equal(
                    nameof(FavoriteMembershipUpdateMessageComposer),
                    nameof(GuildMembershipRejectedMessageComposer),
                    nameof(ExtendedProfileChangedMessageComposer),
                    nameof(GroupDetailsChangedMessageComposer)
                );
        });

    [Fact]
    public Task Holds_off_sends_in_the_order_turbo_used_to() =>
        SessionHarness.InTurns(async () =>
        {
            await OnlineInRoomAsync(memberWearing: true, holdMs: 0);
            var group = await GroupAsync();

            await group.KickAsync(PLAYER, PLAYER, false, Ct);
            await RoomPublishesAsync();
            await SentAsync(4);

            Received()
                .Should()
                .Equal(
                    nameof(GuildMembershipRejectedMessageComposer),
                    nameof(ExtendedProfileChangedMessageComposer),
                    nameof(GroupDetailsChangedMessageComposer),
                    nameof(FavoriteMembershipUpdateMessageComposer)
                );
        });

    [Fact]
    public Task A_room_publish_that_never_comes_holds_the_player_no_longer_than_configured() =>
        SessionHarness.InTurns(async () =>
        {
            var player = await OnlineInRoomAsync(memberWearing: false, holdMs: 50);

            await player.WearIfNoneAsync(GuildId.Parse(GROUP), Ct);
            await SendProfileChangedAsync();
            await Task.Delay(100, Ct);

            // The grain timer does not run without a silo; the next send finds the hold overdue.
            await _hotel.PresenceGrain.SendComposerAsync(
                new GroupDetailsChangedMessageComposer { GuildId = GROUP },
                Ct
            );
            await SentAsync(2);

            Received()
                .Should()
                .Equal(
                    nameof(ExtendedProfileChangedMessageComposer),
                    nameof(GroupDetailsChangedMessageComposer)
                );
        });

    [Fact]
    public Task Leaving_the_room_lets_what_waited_go() =>
        SessionHarness.InTurns(async () =>
        {
            var player = await OnlineInRoomAsync(memberWearing: false);

            await player.WearIfNoneAsync(GuildId.Parse(GROUP), Ct);
            await SendProfileChangedAsync();
            await _hotel.PresenceGrain.ClearActiveRoomAsync(Ct);
            await SentAsync(1);

            Received().Should().Equal(nameof(ExtendedProfileChangedMessageComposer));
        });

    [Fact]
    public Task Concurrent_badge_changes_lose_nothing_and_keep_the_send_order() =>
        SessionHarness.InTurns(async () =>
        {
            var player = await OnlineInRoomAsync(memberWearing: false);

            // Two changes in flight at once (join, then take it off and on), each followed by a
            // reply; the room answers them out of order. Nothing goes before both are answered.
            await player.WearIfNoneAsync(GuildId.Parse(GROUP), Ct);
            await _hotel.PresenceGrain.SendComposerAsync(
                new ExtendedProfileChangedMessageComposer { UserId = 1 },
                Ct
            );
            await player.SetFavouriteGuildAsync(null, Ct);
            await _hotel.PresenceGrain.SendComposerAsync(
                new ExtendedProfileChangedMessageComposer { UserId = 2 },
                Ct
            );

            _roomTold.Should().HaveCount(2);
            _roomTold.Reverse();
            await RoomPublishesAsync();
            await SentAsync(4);

            var received = ReceivedComposers();
            received.Should().HaveCount(4);
            received
                .Take(2)
                .Should()
                .AllBeOfType<FavoriteMembershipUpdateMessageComposer>("the room's go as they come");
            received
                .Skip(2)
                .Cast<ExtendedProfileChangedMessageComposer>()
                .Select(x => x.UserId)
                .Should()
                .Equal(1, 2);
        });

    [Fact]
    public async Task The_room_ends_the_hold_with_its_badge_publish_or_at_once_for_a_player_not_there()
    {
        var room = new WiredRoom();
        room.Harness.Room._roomOutbound = room.Harness.Fakes.Create<
            IAsyncStream<RoomOutboundSnapshot>
        >("stream");
        var here = room.Enter(2, 1, 1).PlayerId;
        var hold = new OutboundHold(here, 7);
        var away = new OutboundHold(PlayerId.Parse(55), 8);

        await room.Harness.Room.SetPlayerFavouriteGuildAsync(
            here,
            -1,
            WornGuildStatus.None,
            "",
            hold,
            Ct
        );
        await room.Harness.Room.SetPlayerFavouriteGuildAsync(
            away.PlayerId,
            -1,
            WornGuildStatus.None,
            "",
            away,
            Ct
        );

        var published = room.Harness.Fakes.RoomPublishes().Should().ContainSingle().Subject;
        published
            .Composers.Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<FavoriteMembershipUpdateMessageComposer>();
        published.ReleasesHold.Should().Be(hold);
        room.Harness.Fakes.Log.Of("ReleaseHoldAsync").Select(x => x.Args[0]).Should().Equal(away);
    }

    /// <summary>
    /// No deadlock by construction: the two new presence calls are tells (interleaved, so any
    /// grain may await them even while the presence waits on it) that call no grain themselves,
    /// and the room is still told, not awaited.
    /// </summary>
    [Fact]
    public void Holds_are_tells_that_call_no_grain()
    {
        foreach (var name in new[] { "HoldForRoomAsync", "ReleaseHoldAsync" })
        {
            typeof(IPlayerPresenceGrain)
                .GetMethod(name)!
                .GetCustomAttributes(typeof(Orleans.Concurrency.AlwaysInterleaveAttribute), false)
                .Should()
                .NotBeEmpty(name);

            var presence = _hotel.Presence.GetType();
            var calls = new HashSet<System.Reflection.MethodBase>();
            var seen = new HashSet<System.Reflection.MethodBase>();
            var todo = new Stack<System.Reflection.MethodBase>([presence.GetMethod(name)!]);

            while (todo.TryPop(out var method))
            {
                if (!seen.Add(method))
                    continue;

                var found = new HashSet<System.Reflection.MethodBase>();
                IlScanner.Scan(method, new HashSet<string>(), found);
                calls.UnionWith(found);

                foreach (var own in found.Where(m => m.DeclaringType == presence))
                    todo.Push(own);
            }

            calls
                .Where(m =>
                    m.DeclaringType is { IsInterface: true } t
                    && typeof(Orleans.Runtime.IAddressable).IsAssignableFrom(t)
                )
                .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
                .Should()
                .BeEmpty(name);
        }
    }

    private async Task<IPlayerGuildGrain> OnlineInRoomAsync(bool memberWearing, int holdMs = 60_000)
    {
        if (memberWearing)
            _db.Insert(
                new GuildMemberEntity
                {
                    Id = 2,
                    GuildEntityId = GROUP,
                    PlayerEntityId = PLAYER,
                    Rank = GuildMemberRank.Member,
                    IsFavourite = true,
                }
            );
        else
            _db.Insert(
                new GuildMemberEntity
                {
                    Id = 2,
                    GuildEntityId = GROUP,
                    PlayerEntityId = PLAYER,
                    Rank = GuildMemberRank.Member,
                    IsFavourite = false,
                }
            );

        GrainHarness.Replace<PlayerConfig>(
            _hotel.Presence,
            new PlayerConfig { OutboundHoldMs = holdMs }
        );

        _session = _hotel.NewSession(1);
        _key = _session.SessionKey;
        await _hotel.Gateway.AddSessionAsync(_key, _session);
        await _hotel.Gateway.AddSessionToPlayerAsync(_key, PLAYER);
        _hotel.Presence._state.ActiveRoomId = ROOM;
        // The login flush finishes before the test starts.
        await SentAsync(0);
        _hotel.Fakes.Log.Calls.Clear();

        var grain = GrainHarness.Create<PlayerGuildGrain>(_hotel.Fakes, _db, PLAYER);
        await ((Grain)grain).OnActivateAsync(Ct);

        _hotel.Fakes.Instances[(typeof(IPlayerGuildGrain), (object)(long)PLAYER)] = grain;
        _hotel.Fakes.Instances[(typeof(IPlayerGuildGrain), (object)PLAYER)] = grain;

        return (IPlayerGuildGrain)grain;
    }

    private async Task<IGuildGrain> GroupAsync()
    {
        var grain = GrainHarness.Create<GuildGrain>(_hotel.Fakes, _db);

        GrainHarness.Replace(grain, new GuildLiveState { GuildId = new GuildId(GROUP) });
        await ((Grain)grain).OnActivateAsync(Ct);

        return (IGuildGrain)grain;
    }

    private MessageContext Ctx() => new(_session, PLAYER, ROOM);

    private Task SendProfileChangedAsync() =>
        _hotel
            .Fakes.Create<IGrainFactory>()
            .SendComposerToPlayerAsync(
                PLAYER,
                new ExtendedProfileChangedMessageComposer { UserId = PLAYER },
                Ct
            );

    /// <summary>The room's badge publish for each change it was told of, as the stream brings it.</summary>
    private async Task RoomPublishesAsync()
    {
        var stream = (IAsyncObserver<RoomOutboundSnapshot>)_hotel.Presence;

        foreach (var call in _roomTold.ToList())
            await stream.OnNextAsync(
                new RoomOutboundSnapshot
                {
                    RoomId = ROOM,
                    Composers =
                    [
                        new FavoriteMembershipUpdateMessageComposer
                        {
                            RoomIndex = 0,
                            GuildId = (int)call.Args[1]!,
                            Status = (WornGuildStatus)call.Args[2]!,
                            GuildName = (string)call.Args[3]!,
                        },
                    ],
                    ReleasesHold = (OutboundHold)call.Args[4]!,
                }
            );
    }

    private List<IComposer> ReceivedComposers() => _hotel.Received(_key);

    /// <summary>
    /// The tests run in turns (<see cref="SessionHarness.InTurns"/>), so the presence flushes
    /// whenever the test awaits: this lets everything already queued run first, then waits (5 s
    /// at most, never a fixed time) for <paramref name="count"/> composers with nothing left to
    /// send. Count 0 is "nothing went": what is held stays held, not in flight.
    /// </summary>
    private async Task SentAsync(int count)
    {
        await Task.Yield();

        for (
            var i = 0;
            i < 500 && !(ReceivedComposers().Count >= count && _hotel.PresenceIdle());
            i++
        )
            await Task.Delay(10, Ct);
    }

    private List<string> Received() => [.. ReceivedComposers().Select(x => x.GetType().Name)];
}
