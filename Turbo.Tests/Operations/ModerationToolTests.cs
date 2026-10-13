using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Operations;
using Turbo.Operations.Configuration;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Texts;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Operations;

/// <summary>
/// The moderation tool, from the client's bytes to the replies its windows read (1318 AS3,
/// <c>com/sulake/habbo/moderation</c>): <c>ModeratorInitData</c> opens the start panel,
/// <c>ModeratorUserInfoData</c> fills the user info, <c>RoomModerationData</c> the room tool, a
/// chatlog record (type byte, short count of typed context entries, short count of lines) the
/// chatlog, and <c>ModeratorActionResult</c> (user id, success) answers a message or caution.
/// </summary>
public sealed class ModerationToolTests : IDisposable
{
    private const int MODERATOR = 1;
    private const int PLAYER = 2;
    private const int ROOM = 9;

    private static readonly DateTime NOW = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly HashSet<string> _held = [];
    private readonly HashSet<string> _targetHeld = [];
    private readonly List<int> _inRoom = [MODERATOR, PLAYER];
    private readonly List<(PlayerId Player, IComposer Composer)> _delivered = [];
    private bool _online = true;

    public ModerationToolTests()
    {
        _db.Insert(Player(MODERATOR, "moderator", NOW.AddDays(-30)));
        _db.Insert(Player(PLAYER, "troll", NOW.AddDays(-2)));
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
        _db.Insert(Room(ROOM, "Troll Room", PLAYER));
        _db.Insert(Line(ROOM, PLAYER, "first", NOW.AddMinutes(-3)));
        _db.Insert(Line(ROOM, MODERATOR, "please stop", NOW.AddMinutes(-2)));
        _db.Insert(Line(ROOM, PLAYER, "no", NOW.AddMinutes(-1)));

        _fakes.Handlers["HasAsync"] = call =>
            Task.FromResult(
                (Convert.ToInt32(call.Key) == MODERATOR ? _held : _targetHeld).Contains(
                    (string)call.Args[0]!
                )
            );
        _fakes.Handlers["GetRoomPlayersAsync"] = _ =>
            Task.FromResult(ImmutableArray.CreateRange(_inRoom.Select(x => (PlayerId)x)));
        _fakes.Handlers["GetRoomPopulationAsync"] = _ => Task.FromResult(_inRoom.Count);
        _fakes.Handlers["GetActiveRoomAsync"] = _ =>
            Task.FromResult(
                new RoomPointerSnapshot { RoomId = ROOM, ActiveSinceUtc = NOW.AddHours(-1) }
            );
        _fakes.Handlers["GetOnlinePlayerIds"] = _ =>
            (IReadOnlyCollection<PlayerId>)(_online ? [PLAYER] : []);
        _fakes.Handlers["TrySendComposerAsync"] = call =>
        {
            _delivered.Add((Convert.ToInt32(call.Key), (IComposer)call.Args[0]!));
            return Task.FromResult(_online);
        };
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task A_player_without_the_tool_gets_no_init()
    {
        (await Service().GetInitAsync(MODERATOR, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task The_init_offers_each_action_its_command_node_grants()
    {
        _held.UnionWith([
            PermissionNodes.Moderation.TOOL,
            PermissionNodes.Command.WARN,
            PermissionNodes.Command.BAN,
        ]);

        var init = await Service().GetInitAsync(MODERATOR, CancellationToken.None);
        var reply = PacketHarness.Encode(new ModeratorInitMessageComposer { Init = init! });

        reply.PopInt().Should().Be(0, "no issues");
        reply.PopInt().Should().Be(1, "one message template");
        reply.PopString().Should().Be("Please behave.");
        reply.PopInt().Should().Be(0, "the list ModeratorInitData drops");
        // cfh, chatlogs, alert, kick, ban, room alert, room kick.
        Enumerable
            .Range(0, 7)
            .Select(_ => reply.PopBoolean())
            .Should()
            .Equal(true, true, true, false, true, false, false);
        reply.PopInt().Should().Be(1, "one room template");
        reply.PopString().Should().Be("Room closed.");
        reply.End.Should().BeTrue();
    }

    [Fact]
    public async Task User_info_is_read_in_the_order_ModeratorUserInfoData_reads_it()
    {
        var reply = Assert.Single(
            await Harness()
                .SendAsync(
                    PacketHarness.Incoming("GetModeratorUserInfoMessageEvent"),
                    PacketHarness.Payload(w => w.Int(PLAYER)),
                    playerId: MODERATOR
                )
        );

        reply.Header.Should().Be(PacketHarness.Outgoing("ModeratorUserInfoMessageComposer"));
        reply.PopInt().Should().Be(PLAYER);
        reply.PopString().Should().Be("troll");
        reply.PopString().Should().Be("hd-180-1");
        reply.PopInt().Should().Be(2 * 24 * 60, "registered two days ago, in minutes");
        reply.PopInt().Should().Be(60, "last login an hour ago, in minutes");
        reply.PopBoolean().Should().BeTrue("online");
        Enumerable.Range(0, 5).Select(_ => reply.PopInt()).Should().Equal(0, 0, 0, 0, 0);
        reply.PopString().Should().BeEmpty("no trade lock");
        reply.PopString().Should().BeEmpty("no purchase date kept");
        reply.PopInt().Should().Be(0);
        reply.PopInt().Should().Be(0);
        reply.PopString().Should().BeEmpty("no email shown");
        reply.PopString().Should().BeEmpty();
        reply.PopString().Should().BeEmpty("never sanctioned");
        reply.PopInt().Should().Be(int.MaxValue);
        reply.End.Should().BeTrue();
    }

    [Fact]
    public async Task Room_info_carries_the_room_and_its_owner()
    {
        var reply = Assert.Single(
            await Harness()
                .SendAsync(
                    PacketHarness.Incoming("GetModeratorRoomInfoMessageEvent"),
                    PacketHarness.Payload(w => w.Int(ROOM)),
                    playerId: MODERATOR
                )
        );

        reply.Header.Should().Be(PacketHarness.Outgoing("ModeratorRoomInfoMessageComposer"));
        reply.PopInt().Should().Be(ROOM);
        reply.PopInt().Should().Be(2, "the live room's count, not the stored UsersNow (3)");
        reply.PopBoolean().Should().BeTrue("the owner is in the room");
        reply.PopInt().Should().Be(PLAYER);
        reply.PopString().Should().Be("troll");
        reply.PopBoolean().Should().BeTrue("exists");
        reply.PopString().Should().Be("Troll Room");
        reply.PopString().Should().Be("a room");
        reply.PopInt().Should().Be(2);
        reply.PopString().Should().Be("fun");
        reply.PopString().Should().Be("chat");
        reply.End.Should().BeTrue();
    }

    [Fact]
    public async Task A_room_that_is_gone_is_only_its_flag()
    {
        var reply = Assert.Single(
            await Harness()
                .SendAsync(
                    PacketHarness.Incoming("GetModeratorRoomInfoMessageEvent"),
                    PacketHarness.Payload(w => w.Int(404)),
                    playerId: MODERATOR
                )
        );

        reply.PopInt().Should().Be(404);
        reply.PopInt();
        reply.PopBoolean();
        reply.PopInt();
        reply.PopString();
        reply.PopBoolean().Should().BeFalse();
        reply.End.Should().BeTrue();
    }

    [Fact]
    public async Task A_room_chatlog_is_one_record_oldest_line_first()
    {
        var reply = Assert.Single(
            await Harness()
                .SendAsync(
                    PacketHarness.Incoming("GetRoomChatlogMessageEvent"),
                    PacketHarness.Payload(w => w.Int(0).Int(ROOM)),
                    playerId: MODERATOR
                )
        );

        reply.Header.Should().Be(PacketHarness.Outgoing("RoomChatlogMessageComposer"));
        var lines = ReadRecord(reply);
        reply.End.Should().BeTrue();

        lines
            .Should()
            .Equal(
                ("11:57", PLAYER, "troll", "first"),
                ("11:58", MODERATOR, "moderator", "please stop"),
                ("11:59", PLAYER, "troll", "no")
            );
    }

    [Fact]
    public async Task A_user_chatlog_holds_only_their_lines_per_room()
    {
        var reply = Assert.Single(
            await Harness()
                .SendAsync(
                    PacketHarness.Incoming("GetUserChatlogMessageEvent"),
                    PacketHarness.Payload(w => w.Int(PLAYER)),
                    playerId: MODERATOR
                )
        );

        reply.Header.Should().Be(PacketHarness.Outgoing("UserChatlogMessageComposer"));
        reply.PopInt().Should().Be(PLAYER);
        reply.PopString().Should().Be("troll");
        reply.PopInt().Should().Be(1, "one room");
        ReadRecord(reply).Select(x => x.Message).Should().Equal("first", "no");
        reply.End.Should().BeTrue();
    }

    [Fact]
    public async Task A_message_reaches_an_online_player_and_is_answered_as_done()
    {
        var reply = await ModMessageAsync("Stop that.");

        _delivered.Should().ContainSingle();
        _delivered[0]
            .Composer.Should()
            .BeOfType<ModeratorMessageComposer>()
            .Which.Message.Should()
            .Be("Stop that.");
        reply.PopInt().Should().Be(PLAYER);
        reply.PopBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task A_message_to_an_offline_player_is_answered_as_failed()
    {
        _online = false;

        var reply = await ModMessageAsync("Stop that.");

        reply.PopInt().Should().Be(PLAYER);
        reply.PopBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task A_caution_needs_the_warn_node()
    {
        var refused = await ModAlertAsync();
        _held.Add(PermissionNodes.Command.WARN);
        var sent = await ModAlertAsync();

        refused.PopInt().Should().Be(PLAYER);
        refused.PopBoolean().Should().BeFalse();
        sent.PopInt().Should().Be(PLAYER);
        sent.PopBoolean().Should().BeTrue();
        _delivered
            .Should()
            .ContainSingle()
            .Which.Composer.Should()
            .BeOfType<ModeratorCautionEventMessageComposer>();
    }

    [Fact]
    public async Task The_room_tool_cautions_everyone_in_the_moderators_room()
    {
        // RoomToolCtrl.act: ModeratorAction(0 = caution, message, ""), no room id.
        var payload = PacketHarness.Payload(w => w.Int(0).String("Calm down.").String(""));

        (
            await Harness()
                .SendAsync(
                    PacketHarness.Incoming("ModeratorActionMessageEvent"),
                    payload,
                    playerId: MODERATOR,
                    roomId: ROOM
                )
        )
            .Should()
            .BeEmpty("the tool closes on send");
        _delivered.Should().BeEmpty("refused without command.roomalert");

        _held.Add(PermissionNodes.Command.ROOMALERT);
        await Harness()
            .SendAsync(
                PacketHarness.Incoming("ModeratorActionMessageEvent"),
                payload,
                playerId: MODERATOR,
                roomId: ROOM
            );

        _delivered
            .Should()
            .ContainSingle("everyone in the room but the moderator")
            .Which.Should()
            .Match<(PlayerId Player, IComposer Composer)>(x =>
                x.Player == PLAYER
                && x.Composer is ModeratorCautionEventMessageComposer
                && ((ModeratorCautionEventMessageComposer)x.Composer).Message == "Calm down."
            );
        _fakes.Log.Of("ModerateRoomBySystemAsync").Should().BeEmpty("action 0 kicks nobody");
    }

    [Fact]
    public async Task A_room_message_with_kick_needs_the_room_kick_node()
    {
        _held.Add(PermissionNodes.Command.ROOMALERT);
        // Action 4: message and kick.
        var payload = PacketHarness.Payload(w => w.Int(4).String("Closing.").String(""));

        await Harness()
            .SendAsync(
                PacketHarness.Incoming("ModeratorActionMessageEvent"),
                payload,
                playerId: MODERATOR,
                roomId: ROOM
            );
        _delivered.Should().BeEmpty();

        _held.Add(PermissionNodes.Command.ROOMKICKALL);
        await Harness()
            .SendAsync(
                PacketHarness.Incoming("ModeratorActionMessageEvent"),
                payload,
                playerId: MODERATOR,
                roomId: ROOM
            );

        _delivered
            .Should()
            .ContainSingle()
            .Which.Composer.Should()
            .BeOfType<ModeratorMessageComposer>();
        var kick = _fakes.Log.Of("ModerateRoomBySystemAsync").Should().ContainSingle().Subject;
        Convert.ToInt32(kick.Key).Should().Be(ROOM);
        kick.Args[0].Should().Be((PlayerId)MODERATOR, "the moderator stays");
        kick.Args.Skip(1).Take(3).Should().Equal(false, null, true);
    }

    [Fact]
    public async Task The_room_tools_checkboxes_lock_rename_and_kick()
    {
        // RoomToolCtrl.act: ModerateRoom(flatId, lock, changeName, kick) as ints.
        var payload = PacketHarness.Payload(w => w.Int(ROOM).Int(1).Int(1).Int(1));

        await Harness()
            .SendAsync(
                PacketHarness.Incoming("ModerateRoomMessageEvent"),
                payload,
                playerId: MODERATOR
            );
        _held.Add(PermissionNodes.Command.ROOMALERT);
        await Harness()
            .SendAsync(
                PacketHarness.Incoming("ModerateRoomMessageEvent"),
                payload,
                playerId: MODERATOR
            );
        _fakes.Log.Of("ModerateRoomBySystemAsync").Should().BeEmpty("kick needs roomkickall");

        _held.Add(PermissionNodes.Command.ROOMKICKALL);
        await Harness()
            .SendAsync(
                PacketHarness.Incoming("ModerateRoomMessageEvent"),
                payload,
                playerId: MODERATOR
            );

        var call = _fakes.Log.Of("ModerateRoomBySystemAsync").Should().ContainSingle().Subject;
        Convert.ToInt32(call.Key).Should().Be(ROOM);
        call.Args.Take(4)
            .Should()
            .Equal((PlayerId)MODERATOR, true, "Inappropriate to hotel management", true);
    }

    [Fact]
    public async Task A_caution_is_counted_in_user_info()
    {
        _held.Add(PermissionNodes.Command.WARN);
        await ModAlertAsync();

        var reply = await UserInfoAsync();

        reply.PopInt().Should().Be(0, "cfh");
        reply.PopInt().Should().Be(0, "abusive cfh");
        reply.PopInt().Should().Be(1, "one caution");
        reply.PopInt().Should().Be(0, "a caution is not a ban");
    }

    [Fact]
    public async Task A_ban_from_the_mod_action_window_lasts_its_type_and_sends_the_player_out()
    {
        // ModBan(user, message, topic, sanctionTypeId 4 = "Ban 7 days", avatarOnly false), no issue.
        var payload = PacketHarness.Payload(w =>
            w.Int(PLAYER).String("Scamming.").Int(12).Int(4).Bool(false)
        );
        _held.Add(PermissionNodes.Command.BAN);
        _targetHeld.Add(PermissionNodes.Command.BAN);

        var refused = await ResultAsync("ModBanMessageEvent", payload);
        _targetHeld.Clear();
        var banned = await ResultAsync("ModBanMessageEvent", payload);

        refused.Should().BeFalse("staff who may ban are out of reach");
        banned.Should().BeTrue();
        using var db = _db.CreateDbContext();
        var ban = db.PlayerSanctions.Single(x =>
            x.Kind == Primitives.Moderation.Enums.SanctionKind.Ban
        );
        ban.PlayerEntityId.Should().Be(PLAYER);
        ban.Reason.Should().Be("Scamming.");
        ban.ExpiresAt.Should().Be(NOW.AddDays(7));
        _fakes.Log.Of("DisconnectPlayerAsync").Should().ContainSingle();
    }

    [Fact]
    public async Task Mute_and_trade_lock_deny_their_nodes_for_their_length()
    {
        _held.UnionWith([PermissionNodes.Command.SILENCE, PermissionNodes.Command.TRADELOCK]);
        _fakes.Handlers["SetNodeAsync"] = _ => Task.FromResult(PermissionChangeResultType.Changed);

        var muted = await ResultAsync(
            "ModMuteMessageEvent",
            PacketHarness.Payload(w => w.Int(PLAYER).String("").Int(12))
        );
        // "Lock trade 1 week": 168 hours * 60.
        var locked = await ResultAsync(
            "ModTradingLockMessageEvent",
            PacketHarness.Payload(w => w.Int(PLAYER).String("").Int(168 * 60).Int(12))
        );

        muted.Should().BeTrue();
        locked.Should().BeTrue();
        _fakes
            .Log.Of("SetNodeAsync")
            .Select(x => (Convert.ToInt32(x.Key), x.Args[0], x.Args[1], x.Args[2]))
            .Should()
            .Equal(
                (PLAYER, PermissionNodes.Chat.SPEAK, false, NOW.AddHours(1)),
                (PLAYER, PermissionNodes.TRADE, false, NOW.AddDays(7))
            );
    }

    [Fact]
    public async Task A_kick_sends_the_player_out_of_the_room_they_are_in()
    {
        _held.Add(PermissionNodes.Room.MODERATE_ANY);
        _fakes.Handlers["KickPlayerAsync"] = _ => Task.FromResult(true);

        var kicked = await ResultAsync(
            "ModKickMessageEvent",
            PacketHarness.Payload(w => w.Int(PLAYER).String("").Int(12))
        );

        kicked.Should().BeTrue();
        var call = _fakes.Log.Of("KickPlayerAsync").Should().ContainSingle().Subject;
        call.Args[0].Should().Be(Primitives.Action.ActionContext.CreateForPlayer(MODERATOR, ROOM));
        call.Args[1].Should().Be((PlayerId)PLAYER);
    }

    [Fact]
    public async Task User_info_counts_trade_locks_and_shows_the_one_in_force()
    {
        _db.Insert(TradeLockAudit(1, NOW.AddDays(-30)));
        _db.Insert(TradeLockAudit(2, NOW.AddDays(3)));
        _db.Insert(
            new Turbo.Database.Entities.Permissions.PlayerPermissionNodeEntity
            {
                PlayerEntityId = PLAYER,
                Node = PermissionNodes.TRADE,
                Value = false,
                ExpiresAt = NOW.AddDays(3),
                IsTemporary = true,
            }
        );

        var reply = await UserInfoAsync();

        Enumerable.Range(0, 5).Select(_ => reply.PopInt()).Should().Equal(0, 0, 0, 0, 2);
        reply.PopString().Should().Be("2026-10-16 12:00");
    }

    private static Turbo.Database.Entities.Permissions.PermissionAuditEntity TradeLockAudit(
        int id,
        DateTime expires
    ) =>
        new()
        {
            Id = id,
            ActorPlayerId = MODERATOR,
            TargetType = Primitives.Players.Enums.PermissionAuditTargetType.Player,
            TargetId = PLAYER,
            Action = Primitives.Players.Enums.PermissionAuditActionType.NodeSet,
            Subject = PermissionNodes.TRADE,
            Value = "false",
            ExpiresAt = expires,
        };

    /// <summary>User info for the target, read up to its five counts.</summary>
    private async Task<ClientPacket> UserInfoAsync()
    {
        var reply = Assert.Single(
            await Harness()
                .SendAsync(
                    PacketHarness.Incoming("GetModeratorUserInfoMessageEvent"),
                    PacketHarness.Payload(w => w.Int(PLAYER)),
                    playerId: MODERATOR
                )
        );
        reply.PopInt();
        reply.PopString();
        reply.PopString();
        reply.PopInt();
        reply.PopInt();
        reply.PopBoolean();
        return reply;
    }

    /// <summary>Sends a mod action and reads its ModeratorActionResult for the target.</summary>
    private async Task<bool> ResultAsync(string message, byte[] payload)
    {
        var reply = Assert.Single(
            await Harness().SendAsync(PacketHarness.Incoming(message), payload, playerId: MODERATOR)
        );
        reply.Header.Should().Be(PacketHarness.Outgoing("ModeratorActionResultMessageComposer"));
        reply.PopInt().Should().Be(PLAYER);
        return reply.PopBoolean();
    }

    private async Task<ClientPacket> ModMessageAsync(string text)
    {
        var reply = Assert.Single(
            await Harness()
                .SendAsync(
                    PacketHarness.Incoming("ModMessageMessageEvent"),
                    // SendMsgsCtrl: user, message, "", "", topic -999, no issue.
                    PacketHarness.Payload(w =>
                        w.Int(PLAYER).String(text).String("").String("").Int(-999)
                    ),
                    playerId: MODERATOR
                )
        );
        reply.Header.Should().Be(PacketHarness.Outgoing("ModeratorActionResultMessageComposer"));
        return reply;
    }

    private async Task<ClientPacket> ModAlertAsync()
    {
        var harness = Harness();
        var replies = await harness.SendAsync(
            PacketHarness.Incoming("ModAlertMessageEvent"),
            PacketHarness.Payload(w => w.Int(PLAYER).String("Caution.").Int(12)),
            playerId: MODERATOR
        );
        return Assert.Single(replies);
    }

    private static List<(string Time, int Id, string Name, string Message)> ReadRecord(
        ClientPacket reply
    )
    {
        reply.PopByte().Should().Be(1, "a room record");
        var context = new Dictionary<string, object>();

        for (int i = 0, n = reply.PopShort(); i < n; i++)
        {
            var key = reply.PopString();
            context[key] = reply.PopByte() switch
            {
                0 => reply.PopBoolean(),
                1 => reply.PopInt(),
                _ => reply.PopString(),
            };
        }

        context.Should().Contain("roomId", ROOM).And.Contain("roomName", "Troll Room");

        var lines = new List<(string, int, string, string)>();

        for (int i = 0, n = reply.PopShort(); i < n; i++)
        {
            lines.Add((reply.PopString(), reply.PopInt(), reply.PopString(), reply.PopString()));
            reply.PopBoolean().Should().BeFalse();
        }

        return lines;
    }

    private PacketHarness Harness()
    {
        var harness = new PacketHarness();
        harness.Resolver.Overrides[typeof(IModerationToolService)] = Service();
        return harness;
    }

    private ModerationToolService Service() =>
        new(
            _db,
            _fakes.Create<Orleans.IGrainFactory>(),
            _fakes.Create<ISessionGateway>(),
            new SanctionService(
                _db,
                new ManualTimeProvider(new DateTimeOffset(NOW)),
                new TestEventBus().System,
                NullLogger<ISanctionService>.Instance
            ),
            _fakes.Create<IRoomService>(),
            _fakes.Create<IHotelTextProvider>(),
            new ManualTimeProvider(new DateTimeOffset(NOW)),
            Options.Create(
                new OperationsConfig
                {
                    ModerationMessageTemplates = ["Please behave."],
                    ModerationRoomMessageTemplates = ["Room closed."],
                }
            ),
            NullLogger<ModerationToolService>.Instance
        );

    private static PlayerEntity Player(int id, string name, DateTime createdAt) =>
        new()
        {
            Id = id,
            Name = name,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            PlayerStatus = PlayerStatusType.Offline,
            CreatedAt = createdAt,
            LastLoginAt = NOW.AddHours(-1),
        };

    private static RoomEntity Room(int id, string name, int owner) =>
        new()
        {
            Id = id,
            Name = name,
            Description = "a room",
            Tags = "fun,chat",
            PlayerEntityId = owner,
            RoomModelEntityId = 1,
            DoorMode = RoomDoorModeType.Open,
            UsersNow = 3,
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
        };

    private int _lineId;

    private RoomChatlogEntity Line(int room, int player, string message, DateTime at) =>
        new()
        {
            Id = ++_lineId,
            RoomEntityId = room,
            PlayerEntityId = player,
            Message = message,
            CreatedAt = at,
            RoomEntity = null!,
            PlayerEntity = null!,
        };
}
