using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Turbo.Database.Entities.Moderation;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Operations;
using Turbo.Operations.Configuration;
using Turbo.Primitives.Messages.Outgoing.Help;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Operations;

/// <summary>
/// The moderators' call for help queue and the room visits window, from the client's bytes to the
/// replies its windows read (1318 AS3, <c>com/sulake/habbo/moderation</c>): <c>IssueInfo</c>
/// (<c>IssueInfoMessageParser</c>) keeps every moderator's <c>IssueManager</c> the same, a pick
/// another moderator won is <c>IssuePickFailed</c>, a close tells the reporter
/// (<c>IssueCloseNotification</c>), and <c>RoomVisits</c> is <c>RoomVisitsData</c>.
/// </summary>
public sealed class ModerationIssueQueueTests : IDisposable
{
    private const int MODERATOR = 1;
    private const int OTHER_MODERATOR = 2;
    private const int REPORTER = 3;
    private const int TROLL = 4;
    private const int ROOM = 9;
    private const int TOPIC = 104;

    private static readonly DateTime NOW = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly List<(int Player, IComposer Composer)> _sent = [];

    public ModerationIssueQueueTests()
    {
        _db.Insert(Player(MODERATOR, "mod"));
        _db.Insert(Player(OTHER_MODERATOR, "mod2"));
        _db.Insert(Player(REPORTER, "victim"));
        _db.Insert(Player(TROLL, "troll"));
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
        _db.Insert(Room(ROOM, "Troll Room", TROLL));
        _db.Insert(
            new CfhTopicEntity
            {
                Id = TOPIC,
                Category = "harassment",
                Name = "bullying",
                Consequence = "mods",
                SortOrder = 1,
                Enabled = true,
            }
        );

        // Online: both moderators and the reporter; only the moderators hold the tool.
        _fakes.Handlers["GetOnlinePlayerIds"] = _ =>
            (IReadOnlyCollection<PlayerId>)[MODERATOR, OTHER_MODERATOR, REPORTER];
        _fakes.Handlers["HasAsync"] = call =>
            Task.FromResult(
                // Only the moderators hold the tool, and every node the mod actions need.
                Convert.ToInt32(call.Key)
                    is MODERATOR
                        or OTHER_MODERATOR
            );
        _fakes.Handlers["SendComposerAsync"] = call =>
        {
            _sent.Add((Convert.ToInt32(call.Key), (IComposer)call.Args[0]!));
            return Task.CompletedTask;
        };
        _fakes.Handlers["TrySendComposerAsync"] = call =>
        {
            _sent.Add((Convert.ToInt32(call.Key), (IComposer)call.Args[0]!));
            return Task.FromResult(true);
        };
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task A_new_call_for_help_reaches_every_moderator_as_an_open_issue()
    {
        await SubmitAsync();

        _sent.Select(x => x.Player).Should().Equal(MODERATOR, OTHER_MODERATOR);
        var reply = PacketHarness.Encode(_sent[0].Composer);

        reply.Header.Should().Be(PacketHarness.Outgoing("IssueInfoMessageComposer"));
        ReadIssue(reply)
            .Should()
            .Be(
                (
                    1,
                    ModIssueSnapshot.STATE_OPEN,
                    ModerationIssueService.SOURCE_NORMAL,
                    TOPIC,
                    "victim",
                    TROLL,
                    "troll",
                    0,
                    "",
                    "he is mean"
                )
            );
        reply.End.Should().BeTrue();
    }

    [Fact]
    public async Task ModeratorInit_starts_the_queue_with_the_open_issues()
    {
        await SubmitAsync();

        var init = new ModeratorInitSnapshot
        {
            MessageTemplates = [],
            RoomMessageTemplates = [],
            CfhPermission = true,
            ChatlogsPermission = true,
            AlertPermission = false,
            KickPermission = false,
            BanPermission = false,
            RoomAlertPermission = false,
            RoomKickPermission = false,
            Issues = await Service().GetOpenIssuesAsync(CancellationToken.None),
        };
        var reply = PacketHarness.Encode(new ModeratorInitMessageComposer { Init = init });

        reply.PopInt().Should().Be(1, "one open issue");
        ReadIssue(reply).IssueId.Should().Be(1);
        reply.PopInt().Should().Be(0, "no message templates");
    }

    [Fact]
    public async Task A_pick_goes_to_everyone_and_a_second_moderator_is_told_who_has_it()
    {
        await SubmitAsync();
        _sent.Clear();

        (await PickAsync(MODERATOR)).Should().BeEmpty();
        _sent.Should().HaveCount(2);
        var picked = ReadIssue(PacketHarness.Encode(_sent[0].Composer));
        picked.State.Should().Be(ModIssueSnapshot.STATE_PICKED);
        (picked.PickerId, picked.PickerName).Should().Be((MODERATOR, "mod"));

        _sent.Clear();
        var failed = Assert.Single(await PickAsync(OTHER_MODERATOR, retryCount: 2));

        failed.Header.Should().Be(PacketHarness.Outgoing("IssuePickFailedMessageComposer"));
        failed.PopInt().Should().Be(1);
        failed.PopInt().Should().Be(1, "the issue");
        failed.PopInt().Should().Be(MODERATOR, "who holds it");
        failed.PopString().Should().Be("mod");
        failed.PopBoolean().Should().BeTrue("retry stays on");
        failed.PopInt().Should().Be(3, "one more try counted, so the client stops at 10");
        failed.End.Should().BeTrue();
        _sent.Should().BeEmpty("nothing changed");
    }

    [Fact]
    public async Task A_release_puts_the_issue_back_and_only_its_picker_can()
    {
        await SubmitAsync();
        await PickAsync(MODERATOR);
        _sent.Clear();

        await Harness()
            .SendAsync(
                PacketHarness.Incoming("ReleaseIssuesMessageEvent"),
                PacketHarness.Payload(w => w.Int(1).Int(1)),
                playerId: OTHER_MODERATOR
            );
        _sent.Should().BeEmpty("not theirs");

        await Harness()
            .SendAsync(
                PacketHarness.Incoming("ReleaseIssuesMessageEvent"),
                PacketHarness.Payload(w => w.Int(1).Int(1)),
                playerId: MODERATOR
            );
        var issue = ReadIssue(PacketHarness.Encode(_sent[0].Composer));
        (issue.State, issue.PickerId).Should().Be((ModIssueSnapshot.STATE_OPEN, 0));
    }

    [Fact]
    public async Task Closing_as_abusive_tells_the_reporter_and_counts_in_their_user_info()
    {
        await SubmitAsync();
        await PickAsync(MODERATOR);
        _sent.Clear();

        // CloseIssuesMessageComposer: the reason first, then the ids.
        await Harness()
            .SendAsync(
                PacketHarness.Incoming("CloseIssuesMessageEvent"),
                PacketHarness.Payload(w => w.Int(2).Int(1).Int(1)),
                playerId: MODERATOR
            );

        var closed = _sent.Where(x => x.Composer is IssueInfoMessageComposer).ToList();
        closed.Select(x => x.Player).Should().Equal(MODERATOR, OTHER_MODERATOR);
        ReadIssue(PacketHarness.Encode(closed[0].Composer))
            .State.Should()
            .Be(ModIssueSnapshot.STATE_CLOSED);

        var notice = _sent.Single(x => x.Composer is IssueCloseNotificationMessageComposer);
        notice.Player.Should().Be(REPORTER);
        var bytes = PacketHarness.Encode(notice.Composer);
        bytes.PopInt().Should().Be(2, "abusive: help.cfh.closed.abusive");
        bytes.PopString().Should().BeEmpty("the client's own text");

        var info = await ToolService().GetUserInfoAsync(REPORTER, CancellationToken.None);
        (info!.CfhCount, info.AbusiveCfhCount).Should().Be((1, 1));
        (await Service().GetOpenIssuesAsync(CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task The_issue_chatlog_is_the_chat_sent_with_the_report()
    {
        await SubmitAsync();

        var reply = Assert.Single(
            await Harness()
                .SendAsync(
                    PacketHarness.Incoming("GetCfhChatlogMessageEvent"),
                    PacketHarness.Payload(w => w.Int(1)),
                    playerId: MODERATOR
                )
        );

        reply.Header.Should().Be(PacketHarness.Outgoing("CfhChatlogMessageComposer"));
        // CfhChatlogData: call id, caller, reported, chat record id, then the record.
        Enumerable.Range(0, 4).Select(_ => reply.PopInt()).Should().Equal(1, REPORTER, TROLL, 1);
        reply.PopByte().Should().Be(1, "a room record");
        reply.PopShort().Should().Be(2);
        reply.PopString().Should().Be("roomName");
        reply.PopByte();
        reply.PopString().Should().Be("Troll Room");
        reply.PopString().Should().Be("roomId");
        reply.PopByte();
        reply.PopInt().Should().Be(ROOM);
        reply.PopShort().Should().Be(1);
        reply.PopString();
        reply.PopInt().Should().Be(TROLL);
        reply.PopString().Should().Be("troll");
        reply.PopString().Should().Be("you are bad");
        reply.PopBoolean().Should().BeTrue("the reported player's line");
        reply.End.Should().BeTrue();
    }

    [Fact]
    public async Task The_issue_handler_window_is_kept_in_the_order_it_is_sent()
    {
        await Harness()
            .SendAsync(
                PacketHarness.Incoming("ModToolPreferencesMessageEvent"),
                PacketHarness.Payload(w => w.Int(10).Int(20).Int(400).Int(300)),
                playerId: MODERATOR
            );

        var preferences = await Service().GetPreferencesAsync(MODERATOR, CancellationToken.None);
        var reply = PacketHarness.Encode(
            new ModeratorToolPreferencesEventMessageComposer { Preferences = preferences! }
        );

        // ModeratorToolPreferencesMessageParser: x, y, width, height.
        Enumerable.Range(0, 4).Select(_ => reply.PopInt()).Should().Equal(10, 20, 400, 300);
        (await Service().GetPreferencesAsync(OTHER_MODERATOR, CancellationToken.None))
            .Should()
            .BeNull();
    }

    [Fact]
    public async Task Room_visits_list_the_rooms_entered_newest_first_with_their_time()
    {
        _db.Insert(Visit(1, TROLL, NOW.AddHours(-3).AddMinutes(-5)));
        _db.Insert(Visit(2, TROLL, NOW.AddMinutes(-7)));

        var harness = new PacketHarness();
        harness.Resolver.Overrides[typeof(IModerationToolService)] = ToolService();
        var reply = Assert.Single(
            await harness.SendAsync(
                PacketHarness.Incoming("GetRoomVisitsMessageEvent"),
                PacketHarness.Payload(w => w.Int(TROLL)),
                playerId: MODERATOR
            )
        );

        reply.Header.Should().Be(PacketHarness.Outgoing("RoomVisitsMessageComposer"));
        reply.PopInt().Should().Be(TROLL);
        reply.PopString().Should().Be("troll");
        reply.PopInt().Should().Be(2);
        (reply.PopInt(), reply.PopString(), reply.PopInt(), reply.PopInt())
            .Should()
            .Be((ROOM, "Troll Room", 11, 53));
        (reply.PopInt(), reply.PopString(), reply.PopInt(), reply.PopInt())
            .Should()
            .Be((ROOM, "Troll Room", 8, 55));
        reply.End.Should().BeTrue();
    }

    [Fact]
    public async Task The_reporter_sees_their_pending_call_and_discarding_it_deletes_the_issue()
    {
        await SubmitAsync();
        _sent.Clear();

        var pending = Assert.Single(
            await Harness()
                .SendAsync(
                    PacketHarness.Incoming("GetPendingCallsForHelpMessageEvent"),
                    PacketHarness.Payload(_ => { }),
                    playerId: REPORTER
                )
        );

        // CallForHelpPendingCallsMessageParser: count, then id, time and message as strings.
        pending
            .Header.Should()
            .Be(PacketHarness.Outgoing("CallForHelpPendingCallsMessageComposer"));
        pending.PopInt().Should().Be(1);
        pending.PopString().Should().Be("1");
        pending.PopString().Should().NotBeEmpty();
        pending.PopString().Should().Be("he is mean");
        pending.End.Should().BeTrue();

        var deleted = Assert.Single(
            await Harness()
                .SendAsync(
                    PacketHarness.Incoming("DeletePendingCallsForHelpMessageEvent"),
                    PacketHarness.Payload(_ => { }),
                    playerId: REPORTER
                )
        );

        deleted
            .Header.Should()
            .Be(PacketHarness.Outgoing("CallForHelpPendingCallsDeletedMessageComposer"));
        deleted.End.Should().BeTrue();

        // Every moderator drops it (IssueDeletedMessageParser: the id as a string).
        _sent.Select(x => x.Player).Should().Equal(MODERATOR, OTHER_MODERATOR);
        var removed = PacketHarness.Encode(_sent[0].Composer);
        removed.Header.Should().Be(PacketHarness.Outgoing("IssueDeletedMessageComposer"));
        removed.PopString().Should().Be("1");

        (await Service().GetOpenIssuesAsync(CancellationToken.None)).Should().BeEmpty();
        var empty = Assert.Single(
            await Harness()
                .SendAsync(
                    PacketHarness.Incoming("GetPendingCallsForHelpMessageEvent"),
                    PacketHarness.Payload(_ => { }),
                    playerId: REPORTER
                )
        );
        empty.PopInt().Should().Be(0, "nothing pending: the client goes on to the report");
    }

    [Fact]
    public async Task Closing_with_the_default_sanction_gives_the_reported_player_the_next_step()
    {
        var config = new OperationsConfig { CfhDefaultSanctions = ["ALERT", "BAN:18"] };
        var service = Service(config);

        (await service.GetDefaultSanctionAsync(TROLL, CancellationToken.None))!
            .Name.Should()
            .Be("ALERT");

        await SubmitAsync();
        await PickAsync(MODERATOR);
        _sent.Clear();
        await CloseDefaultAsync(1, config);

        var alert = _sent.Single(x => x.Composer is ModeratorCautionEventMessageComposer);
        alert.Player.Should().Be(TROLL);
        _sent
            .Where(x => x.Composer is IssueCloseNotificationMessageComposer)
            .Select(x => x.Player)
            .Should()
            .Equal(REPORTER);
        (await service.GetOpenIssuesAsync(CancellationToken.None)).Should().BeEmpty();

        // One step up: the next report closed this way bans for 18 hours.
        var next = (await service.GetDefaultSanctionAsync(TROLL, CancellationToken.None))!;
        (next.Name, next.LengthHours).Should().Be(("BAN", 18));

        await SubmitAsync();
        await PickAsync(MODERATOR, issueId: 2);
        await CloseDefaultAsync(2, config);

        var ban = await Sanctions().GetActiveBanAsync(TROLL, CancellationToken.None);
        ban!.ExpiresAtUtc.Should().Be(NOW.AddHours(18));
        ban.Reason.Should().Be("bullying", "the topic of the report");
    }

    [Fact]
    public async Task The_issue_handler_is_told_the_reported_players_next_default_sanction()
    {
        await SubmitAsync();

        // IssueManager.requestSanctionData: ModToolSanctionComposer(issue, -1, topic).
        var reply = Assert.Single(
            await Harness()
                .SendAsync(
                    PacketHarness.Incoming("ModToolSanctionMessageEvent"),
                    PacketHarness.Payload(w => w.Int(1).Int(-1).Int(-1)),
                    playerId: MODERATOR
                )
        );

        // CfhSanctionMessageParser + CfhSanctionTypeData; showDefaultSanction wants the reported player.
        reply.Header.Should().Be(PacketHarness.Outgoing("CfhSanctionMessageComposer"));
        reply.PopInt().Should().Be(1);
        reply.PopInt().Should().Be(TROLL);
        reply.PopString().Should().Be("ALERT");
        reply.PopInt().Should().Be(0, "hours");
        reply.PopInt().Should().Be(0, "type id");
        reply.PopBoolean().Should().BeFalse("not avatar only");
        reply.PopString().Should().BeEmpty("no trade lock");
        reply.PopString().Should().BeEmpty("no machine ban");
        reply.End.Should().BeTrue();
    }

    [Fact]
    public async Task The_mod_action_windows_default_sanction_gives_the_next_step_with_the_moderators_text()
    {
        var config = new OperationsConfig { CfhDefaultSanctions = ["ALERT", "BAN:18"] };

        // ModActionCtrl.onDefaultSanctionButton, opened from the user info: no issue id.
        var reply = Assert.Single(
            await DefaultSanctionAsync(config, w => w.Int(TROLL).Int(TOPIC).String("stop that"))
        );

        var caution = _sent.Single(x => x.Composer is ModeratorCautionEventMessageComposer);
        caution.Player.Should().Be(TROLL);
        ((ModeratorCautionEventMessageComposer)caution.Composer).Message.Should().Be("stop that");
        reply.Header.Should().Be(PacketHarness.Outgoing("ModeratorActionResultMessageComposer"));
        reply.PopInt().Should().Be(TROLL);
        reply.PopBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task A_default_sanction_from_a_held_issue_is_recorded_on_it_and_closes_it()
    {
        var config = new OperationsConfig { CfhDefaultSanctions = ["ALERT", "BAN:18"] };
        await SubmitAsync();
        await PickAsync(MODERATOR);
        _sent.Clear();

        // No text written: the player is told the topic.
        await DefaultSanctionAsync(config, w => w.Int(TROLL).Int(TOPIC).String("").Int(1));

        var caution = _sent.Single(x => x.Composer is ModeratorCautionEventMessageComposer);
        ((ModeratorCautionEventMessageComposer)caution.Composer).Message.Should().Be("bullying");
        _sent
            .Where(x => x.Composer is IssueCloseNotificationMessageComposer)
            .Select(x => x.Player)
            .Should()
            .Equal(REPORTER);
        var service = Service(config);
        (await service.GetOpenIssuesAsync(CancellationToken.None)).Should().BeEmpty();
        var next = (await service.GetDefaultSanctionAsync(TROLL, CancellationToken.None))!;
        (next.Name, next.LengthHours).Should().Be(("BAN", 18));
    }

    [Fact]
    public async Task A_default_sanction_with_none_configured_gives_nothing_and_says_so()
    {
        var reply = Assert.Single(
            await DefaultSanctionAsync(
                new OperationsConfig { CfhDefaultSanctions = [] },
                w => w.Int(TROLL).Int(TOPIC).String("stop that")
            )
        );

        _sent.Should().NotContain(x => x.Composer is ModeratorCautionEventMessageComposer);
        reply.PopInt().Should().Be(TROLL);
        reply.PopBoolean().Should().BeFalse();
    }

    private async Task<List<ClientPacket>> DefaultSanctionAsync(
        OperationsConfig config,
        Action<PayloadWriter> write
    )
    {
        var harness = new PacketHarness();
        harness.Resolver.Overrides[typeof(IModerationIssueService)] = Service(config);

        return await harness.SendAsync(
            PacketHarness.Incoming("DefaultSanctionMessageEvent"),
            PacketHarness.Payload(write),
            playerId: MODERATOR
        );
    }

    private async Task CloseDefaultAsync(int issueId, OperationsConfig config)
    {
        var harness = new PacketHarness();
        harness.Resolver.Overrides[typeof(IModerationIssueService)] = Service(config);

        // CloseIssueDefaultActionMessageComposer: the issue, the others (none), the topic (-1).
        await harness.SendAsync(
            PacketHarness.Incoming("CloseIssueDefaultActionMessageEvent"),
            PacketHarness.Payload(w => w.Int(issueId).Int(0).Int(-1)),
            playerId: MODERATOR
        );
    }

    private CallForHelpService CallForHelp() =>
        new(
            _db,
            Options.Create(new OperationsConfig()),
            _fakes.Create<Turbo.Primitives.Texts.IHotelTextProvider>(),
            new ManualTimeProvider(new DateTimeOffset(NOW)),
            Service()
        );

    private async Task SubmitAsync()
    {
        var result = await CallForHelp()
            .SubmitAsync(
                REPORTER,
                new CfhSubmissionSnapshot
                {
                    Source = CfhSourceType.Room,
                    Message = "he is mean",
                    TopicId = TOPIC,
                    ReportedPlayerId = TROLL,
                    RoomId = ROOM,
                    Name = string.Empty,
                    Email = string.Empty,
                    ChatLines =
                    [
                        new CfhChatLineSnapshot { PlayerId = TROLL, Text = "you are bad" },
                    ],
                },
                CancellationToken.None
            );

        result.Result.Should().Be(CfhResultType.Sent);
    }

    private async Task<List<ClientPacket>> PickAsync(
        int moderator,
        int retryCount = 0,
        int issueId = 1
    ) =>
        await Harness()
            .SendAsync(
                PacketHarness.Incoming("PickIssuesMessageEvent"),
                PacketHarness.Payload(w =>
                    w.Int(1).Int(issueId).Bool(true).Int(retryCount).String("pick")
                ),
                playerId: moderator
            );

    private static (
        int IssueId,
        int State,
        int Source,
        int Topic,
        string Reporter,
        int ReportedId,
        string Reported,
        int PickerId,
        string PickerName,
        string Message
    ) ReadIssue(ClientPacket reply)
    {
        var issueId = reply.PopInt();
        var state = reply.PopInt();
        var source = reply.PopInt();
        var topic = reply.PopInt();
        reply.PopInt().Should().BeGreaterThanOrEqualTo(0, "age in ms");
        reply.PopInt().Should().Be(ModerationIssueService.PRIORITY);
        var grouping = reply.PopInt();
        reply.PopInt().Should().Be(REPORTER);
        var reporter = reply.PopString();
        var reportedId = reply.PopInt();
        var reported = reply.PopString();
        grouping.Should().Be(reportedId > 0 ? 1 : 0);
        var pickerId = reply.PopInt();
        var pickerName = reply.PopString();
        var message = reply.PopString();
        reply.PopInt().Should().Be(issueId, "the chat record is the issue's");
        reply.PopInt().Should().Be(0, "no pattern matches");

        return (
            issueId,
            state,
            source,
            topic,
            reporter,
            reportedId,
            reported,
            pickerId,
            pickerName,
            message
        );
    }

    private PacketHarness Harness()
    {
        var harness = new PacketHarness();
        harness.Resolver.Overrides[typeof(IModerationIssueService)] = Service();
        harness.Resolver.Overrides[typeof(ICallForHelpService)] = CallForHelp();
        return harness;
    }

    private ModerationIssueService Service(OperationsConfig? config = null) =>
        new(
            _db,
            _fakes.Create<Orleans.IGrainFactory>(),
            _fakes.Create<ISessionGateway>(),
            new ManualTimeProvider(new DateTimeOffset(NOW)),
            Options.Create(config ?? new OperationsConfig()),
            ToolService(config),
            _fakes.Create<Turbo.Primitives.Texts.IHotelTextProvider>(),
            new TestEventBus().System
        );

    private ModerationToolService ToolService(OperationsConfig? config = null) =>
        new(
            _db,
            _fakes.Create<Orleans.IGrainFactory>(),
            _fakes.Create<ISessionGateway>(),
            Sanctions(),
            _fakes.Create<Turbo.Primitives.Rooms.IRoomService>(),
            _fakes.Create<Turbo.Primitives.Texts.IHotelTextProvider>(),
            new ManualTimeProvider(new DateTimeOffset(NOW)),
            Options.Create(config ?? new OperationsConfig()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ModerationToolService>.Instance
        );

    private SanctionService Sanctions() =>
        new(
            _db,
            new ManualTimeProvider(new DateTimeOffset(NOW)),
            new TestEventBus().System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ISanctionService>.Instance
        );

    private static PlayerEntity Player(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            PlayerStatus = PlayerStatusType.Offline,
            CreatedAt = NOW.AddDays(-30),
            LastLoginAt = NOW.AddHours(-1),
        };

    private static RoomEntryLogEntity Visit(int id, int player, DateTime at) =>
        new()
        {
            Id = id,
            RoomEntityId = ROOM,
            PlayerEntityId = player,
            CreatedAt = at,
        };

    private static RoomEntity Room(int id, string name, int owner) =>
        new()
        {
            Id = id,
            Name = name,
            Description = "a room",
            PlayerEntityId = owner,
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
        };
}
