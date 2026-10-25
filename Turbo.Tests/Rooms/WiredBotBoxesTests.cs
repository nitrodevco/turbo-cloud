using FluentAssertions;
using Turbo.Primitives.Figures;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Bot;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Avatars.Bot;
using Turbo.Rooms.Object.Logic.Avatar;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The bot boxes as Habbo runs them (official client capture 2026-10-24, own room with two bots
/// "Frank" and "frank", and a second account for the boxes that act on a user; case ids from the
/// W8 sheet). A bot name is matched case ignored and as saved, a space before or after it
/// included, and names every bot of that name: talk, move, teleport and change figure act on all
/// of them, follow, hand item and talk to avatar on one. Ours trimmed the name and took the first
/// bot, fired the reach triggers for any bot with no name, refused a teleport onto a user, handed
/// the item over without the bot, and talked to the room without the user's name.
/// </summary>
public sealed class WiredBotBoxesTests
{
    private const int BOX = 1;
    private const int FRANK = 20;
    private const int FRANK_LOWER = 21;
    private const int USER = 5;
    private const int FURNI = 30;

    private readonly WiredRoom _room = new(12, 12);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // action.23.B1 ("frank ", 2/2), action.23.B1b (6/6 fires), action.23.B1c (" Frank", 2/2).
    [Theory]
    [InlineData("Frank", 2)]
    [InlineData("frank", 2)]
    [InlineData("FRANK", 2)]
    [InlineData("frank ", 0)]
    [InlineData(" Frank", 0)]
    public async Task Bot_talk_names_every_bot_of_the_name_case_ignored_nothing_trimmed(
        string name,
        int talking
    )
    {
        AddBots();
        var talk = await SaveBotBoxAsync<WiredActionBotTalk>(
            "wf_act_bot_talk",
            $"{name}\thello",
            ints: [0, -1, 0]
        );

        await talk.ExecuteAsync(Execution(), Ct);

        _room.Harness.Fakes.SentToRoom<ChatMessageComposer>().Should().HaveCount(talking);
    }

    // action.23.L4 (LAB-W8 wlab9, official client capture 2026-10-25T02-57-3*): twelve Bot Talks
    // in one firing said 10 lines (3/3); a firing 2.1 to 4.6 s after a burst said nothing (3/3),
    // one 5.7 s after it 10 again (2/2). Ours said every line.
    [Fact]
    public async Task A_bot_says_ten_lines_then_nothing_until_five_seconds_have_passed()
    {
        AddBot(FRANK, "Frank", 1, 1);
        var talk = await SaveBotBoxAsync<WiredActionBotTalk>(
            "wf_act_bot_talk",
            "Frank\thi",
            ints: [0, -1, 0]
        );

        async Task<int> TwelveLinesAsync()
        {
            var before = _room.Harness.Fakes.SentToRoom<ChatMessageComposer>().Count();

            for (var i = 0; i < 12; i++)
                await talk.ExecuteAsync(Execution(), Ct);

            return _room.Harness.Fakes.SentToRoom<ChatMessageComposer>().Count() - before;
        }

        (await TwelveLinesAsync()).Should().Be(10);
        _room.Clock.AdvanceMs(4_600);
        (await TwelveLinesAsync()).Should().Be(0);
        _room.Clock.AdvanceMs(1_100);
        (await TwelveLinesAsync()).Should().Be(10);
    }

    // action.23.L3b / trigger.0.L3b (LAB-W8 wlab9, 2026-10-25T02-53..02-55): Says Keyword "hi" +
    // Bot Talk "Frank\thi" on one tile, a user says "hi": the bot's line fires its own stack again,
    // and the loop stops at 10 bot lines (10/10/10/10), not at the 100 executions wired allows.
    [Fact]
    public async Task A_bot_line_firing_its_own_says_stack_stops_at_ten_lines()
    {
        AddBot(FRANK, "Frank", 1, 1);
        _room.Enter(USER, 5, 5, name: "Hutch.");
        _room.AddBox<WiredTriggerHabboSaysKeyword>(BOX, 0, 11, "wf_trg_says_something");
        (await _room.SaveAsync<UpdateTriggerMessage>(BOX, intParams: [0, 0, 0], stringParam: "hi"))
            .Should()
            .BeTrue();
        _room.AddBox<WiredActionBotTalk>(2, 0, 11, "wf_act_bot_talk");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                2,
                intParams: [0, -1, 0],
                playerSources:
                [
                    [WiredPlayerSourceType.BotByName],
                ],
                stringParam: "Frank\thi",
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();
        await Wired.OnRoomEventAsync(
            new Turbo.Primitives.Rooms.Events.Wired.RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = Turbo.Primitives.Action.ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(0, 11)],
            },
            Ct
        );
        await TickAsync(1);

        await Wired.OnRoomEventAsync(
            new Turbo.Primitives.Rooms.Events.Player.PlayerChatEvent
            {
                RoomId = 1,
                CausedBy = _room.ContextFor(USER),
                PlayerId = (Turbo.Primitives.Players.PlayerId)(100 + USER),
                ObjectId = USER,
                ChatType = Turbo.Primitives.Rooms.Enums.RoomChatType.Chat,
                StyleId = 0,
                TrackingId = 0,
                Gesture = default,
                Text = "hi",
            },
            Ct
        );

        (await TickAsync(4)).Should().Be(10);
    }

    // action.25.B1 (2/2, a different bot each run) and action.25.B1b (stop: one bot).
    [Fact]
    public async Task Bot_follow_takes_one_bot_of_the_name()
    {
        var (frank, frankLower) = AddBots();
        _room.Enter(USER, 8, 8);
        var follow = await SaveBotBoxAsync<WiredActionBotFollowAvatar>(
            "wf_act_bot_follow_avatar",
            "Frank",
            ints: [1],
            withUsers: true
        );

        await follow.ExecuteAsync(Execution(USER), Ct);

        new[] { frank.FollowObjectId, frankLower.FollowObjectId }
            .Count(x => x == USER)
            .Should()
            .Be(1);
    }

    // trigger.13.B1 (0/3), trigger.14.B1 (0/2), trigger.13.R1 ("frank " never, "FRANK" fires).
    [Theory]
    [InlineData("", false)]
    [InlineData("frank ", false)]
    [InlineData("FRANK", true)]
    public async Task Bot_reached_triggers_fire_only_for_a_bot_they_name(string name, bool fires)
    {
        var item = _room.AddBox<WiredTriggerBotReachesItem>(BOX, 0, 0, "wf_trg_bot_reached_stf");
        var avatar = _room.AddBox<WiredTriggerBotReachesHabbo>(2, 1, 0, "wf_trg_bot_reached_avtr");
        await _room.SaveAsync<UpdateTriggerMessage>(BOX, stringParam: name);
        await _room.SaveAsync<UpdateTriggerMessage>(2, stringParam: name);

        (
            await item.MatchesEventAsync(
                new BotReachedItemEvent
                {
                    RoomId = 1,
                    CausedBy = Turbo.Primitives.Action.ActionContext.CreateForSystem(1),
                    BotObjectId = FRANK_LOWER,
                    BotName = "frank",
                    FurniId = FURNI,
                },
                Ct
            )
        ).Should().Be(fires);
        (
            await avatar.MatchesEventAsync(
                new BotReachedAvatarEvent
                {
                    RoomId = 1,
                    CausedBy = Turbo.Primitives.Action.ActionContext.CreateForSystem(1),
                    BotObjectId = FRANK_LOWER,
                    BotName = "frank",
                    TargetObjectId = USER,
                },
                Ct
            )
        ).Should().Be(fires);
    }

    // trigger.13.R1: Bot Reached Furni fires for a bot's first step onto any tile of the furni,
    // once per bot, not again for its further steps on the furni (2 fires per run, 2/2), and for
    // a Bot Teleport onto it (R1b). Ours fired only when a bot sent there by Bot Move stopped on
    // it. Here the bot walks there on its own: across the 2x2 rug, then back onto it.
    [Fact]
    public async Task Bot_reached_furni_fires_for_any_bots_step_onto_it()
    {
        var frank = AddBot(FRANK, "Frank", 1, 1);
        frank.SetLogic(new BotLogic(new RoomBotContext(_room.Harness.Room, frank)));
        _room.AddFloorItem(FURNI, 4, 1, width: 2, length: 2);
        _room.AddBox<WiredTriggerBotReachesItem>(BOX, 0, 11, "wf_trg_bot_reached_stf");
        _room.AddBox<WiredActionBotTalk>(2, 0, 11, "wf_act_bot_talk");
        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                BOX,
                stringParam: "Frank",
                stuffIds: [FURNI],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                2,
                intParams: [0, -1, 0],
                playerSources:
                [
                    [WiredPlayerSourceType.BotByName],
                ],
                stringParam: "Frank\treached",
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();
        await Wired.OnRoomEventAsync(
            new Turbo.Primitives.Rooms.Events.Wired.RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = Turbo.Primitives.Action.ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(0, 11)],
            },
            Ct
        );

        var now = 10_000L;

        async Task<int> WalkToAsync(int x, int y)
        {
            (await _room.Harness.Module<RoomAvatarModule>().WalkAvatarToAsync(frank, x, y, Ct))
                .Should()
                .BeTrue();

            for (var i = 0; i < 20; i++)
            {
                now += 500;
                await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(now, Ct);
                await Wired.ProcessWiredAsync(now, dormant: false, Ct);
            }

            (frank.X, frank.Y).Should().Be((x, y));

            return _room.Harness.Fakes.SentToRoom<ChatMessageComposer>().Count();
        }

        (await WalkToAsync(8, 1)).Should().Be(1);
        (await WalkToAsync(4, 2)).Should().Be(2);
    }

    // action.21.B2 (onto the user's tile, 2/2) and action.21.R3 (effect 4 at the fire, set on the
    // furni ~1.5 s later, effect off ~3.1 s after the fire, 3/3).
    [Fact]
    public async Task Bot_teleport_lands_on_a_users_tile_after_its_effect()
    {
        var (frank, frankLower) = AddBots();
        _room.AddFloorItem(FURNI, 3, 9);
        _room.Enter(USER, 3, 9);
        var teleport = await SaveBotBoxAsync<WiredActionBotTeleport>(
            "wf_act_bot_teleport",
            "Frank",
            stuffIds: [FURNI]
        );

        (await teleport.ExecuteAsync(Execution(), Ct)).Should().BeTrue();

        frank.EffectId.Should().Be(4);
        (frank.X, frank.Y).Should().Be((1, 1));

        await Wired.ProcessWiredAsync(1_499, dormant: false, Ct);
        (frank.X, frank.Y).Should().Be((1, 1));

        await Wired.ProcessWiredAsync(1_500, dormant: false, Ct);
        (frank.X, frank.Y).Should().Be((3, 9));
        (frankLower.X, frankLower.Y).Should().Be((3, 9));

        await Wired.ProcessWiredAsync(3_100, dormant: false, Ct);
        frank.EffectId.Should().Be(0);
    }

    // action.22.B2: the chair taken, the bot walks to the tile beside it; a lamp nobody can stand
    // on moves no bot.
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Bot_move_to_a_taken_tile_walks_beside_it_and_to_a_blocked_furni_not_at_all(
        bool walkable,
        bool moves
    )
    {
        var frank = AddBot(FRANK, "Frank", 1, 1);
        _room.AddFloorItem(FURNI, 6, 6, canWalk: walkable);

        if (walkable)
            _room.Enter(USER, 6, 6);

        var move = await SaveBotBoxAsync<WiredActionBotMove>(
            "wf_act_bot_move",
            "Frank",
            stuffIds: [FURNI]
        );

        (await move.ExecuteAsync(Execution(), Ct)).Should().Be(moves);

        frank.IsWalking.Should().Be(moves);

        if (moves)
            _room
                .Map.GetTileXY(frank.GoalTileId)
                .Should()
                .Be((5, 5), "the tile beside it nearest the bot");
    }

    // action.24.B1 (next to the user: bot carries, user gets it, bot empty, one tick) and
    // action.24.R3 (away from the user: the bot carries it and walks to the user first).
    [Theory]
    [InlineData(4, 5, false)]
    [InlineData(1, 1, true)]
    public async Task Bot_give_hand_item_is_carried_over_by_the_bot(int botX, int botY, bool walks)
    {
        var frank = AddBot(FRANK, "Frank", botX, botY);
        var user = _room.Enter(USER, 5, 5);
        var give = await SaveBotBoxAsync<WiredActionBotGiveHandItem>(
            "wf_act_bot_give_handitem",
            "Frank",
            ints: [2],
            withUsers: true
        );

        await give.ExecuteAsync(Execution(USER), Ct);

        var carried = _room
            .Harness.Fakes.SentToRoom<CarryObjectMessageComposer>()
            .Select(x => ((int)x.UserId, x.ItemType));

        if (walks)
        {
            carried.Should().Equal((FRANK, 2));
            frank.IsWalking.Should().BeTrue();
            user.HandItemId.Should().Be(0);
        }
        else
        {
            carried.Should().Equal((FRANK, 2), (USER, 2), (FRANK, 0));
        }
    }

    // action.24.B2: no way to the user (enclosed by furni nobody can stand on): the bot does not
    // walk, the item is handed over at once, in the fire's tick (1/1). Ours kept it in the bot's
    // hand and gave the user nothing.
    [Fact]
    public async Task Bot_give_hand_item_with_no_way_to_the_user_hands_it_over_at_once()
    {
        var frank = AddBot(FRANK, "Frank", 1, 1);
        var user = _room.Enter(USER, 8, 8);

        for (var x = 7; x <= 9; x++)
        for (var y = 7; y <= 9; y++)
            if ((x, y) != (8, 8))
                _room.AddFloorItem(FURNI + x * 10 + y, x, y, canWalk: false);

        var give = await SaveBotBoxAsync<WiredActionBotGiveHandItem>(
            "wf_act_bot_give_handitem",
            "Frank",
            ints: [2],
            withUsers: true
        );

        await give.ExecuteAsync(Execution(USER), Ct);

        _room
            .Harness.Fakes.SentToRoom<CarryObjectMessageComposer>()
            .Select(x => ((int)x.UserId, x.ItemType))
            .Should()
            .Equal((FRANK, 2), (USER, 2), (FRANK, 0));
        user.HandItemId.Should().Be(2);
        frank.IsWalking.Should().BeFalse();
    }

    // trigger.14.R1 / R2: Bot Reached Avatar fires once when the following bot stops beside the
    // user, and again only once it has walked after the user again (official client capture
    // 2026-10-24T18-08-27: one fire 0.5 s after each of two arrivals, none in the 18 s the bot
    // stood beside the user between them; 14-26-38 the same, 1/1). Ours fired every 5 s for as
    // long as the bot stood there.
    [Fact]
    public async Task Bot_reached_avatar_fires_once_per_arrival_of_a_following_bot()
    {
        var frank = AddWalkingBot();
        var user = _room.Enter(USER, 6, 1);
        await SaveReachedAvatarStackAsync();

        (await _room.Harness.Module<RoomBotModule>().FollowAsync(frank, USER, true))
            .Should()
            .BeTrue();

        (await TickAsync(40)).Should().Be(1, "one arrival, then 18 s beside the user");

        await _room
            .Harness.Module<RoomAvatarModule>()
            .RelocateAvatarAsync(user, _room.Map.ToIdx(6, 8), Ct);

        (await TickAsync(40)).Should().Be(2, "the bot followed and arrived once more");
    }

    // trigger.14.X1 / action.25.X1: Bot Follow started with Frank already beside the user: Bot
    // Reached Avatar did not fire (0/2, 6-7 s watched) while the user's walks kept him beside the
    // bot; it fired once the user walked out of reach and the bot caught up (official client
    // capture 2026-10-25T01-13-38, 1/1). Ours fired at the start.
    [Fact]
    public async Task Bot_reached_avatar_waits_for_an_arrival_when_the_follow_starts_beside_the_user()
    {
        var frank = AddWalkingBot();
        var user = _room.Enter(USER, 2, 1);
        await SaveReachedAvatarStackAsync();

        (await _room.Harness.Module<RoomBotModule>().FollowAsync(frank, USER, true))
            .Should()
            .BeTrue();

        (await TickAsync(20)).Should().Be(0, "the bot stood beside the user from the start");

        await _room
            .Harness.Module<RoomAvatarModule>()
            .RelocateAvatarAsync(user, _room.Map.ToIdx(6, 8), Ct);

        (await TickAsync(40)).Should().Be(1, "the bot followed and arrived");
    }

    // trigger.14.R4 / action.24.R3: a bot handing an item over fires Bot Reached Avatar as it
    // arrives beside the user, in the tick it hands the item over (official client capture
    // 2026-10-24T18-02-52: CarryObject user, CarryObject bot 0 and the trigger's flash in one
    // tick, 1/1). Ours raised it only for a following bot.
    [Fact]
    public async Task Bot_reached_avatar_fires_when_a_bot_arrives_to_hand_an_item_over()
    {
        AddWalkingBot();
        var user = _room.Enter(USER, 6, 1);
        await SaveReachedAvatarStackAsync();
        var give = _room.AddBox<WiredActionBotGiveHandItem>(3, 0, 10, "wf_act_bot_give_handitem");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                3,
                intParams: [2],
                playerSources:
                [
                    [WiredPlayerSourceType.BotByName],
                    [WiredPlayerSourceType.SelectorUsers],
                ],
                stringParam: "Frank",
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        await give.ExecuteAsync(Execution(USER), Ct);

        (await TickAsync(20)).Should().Be(1);
        user.HandItemId.Should().Be(2);
    }

    private long _now = 10_000L;

    private RoomBotAvatar AddWalkingBot()
    {
        var frank = AddBot(FRANK, "Frank", 1, 1);

        frank.SetLogic(new BotLogic(new RoomBotContext(_room.Harness.Room, frank)));

        return frank;
    }

    // Bot Reached Avatar "Frank" with a Bot Talk "Frank\treached": each fire is one Chat.
    private async Task SaveReachedAvatarStackAsync()
    {
        _room.AddBox<WiredTriggerBotReachesHabbo>(BOX, 0, 11, "wf_trg_bot_reached_avtr");
        _room.AddBox<WiredActionBotTalk>(2, 0, 11, "wf_act_bot_talk");
        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                BOX,
                stringParam: "Frank",
                playerSources:
                [
                    [WiredPlayerSourceType.BotByName],
                ]
            )
        ).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                2,
                intParams: [0, -1, 0],
                playerSources:
                [
                    [WiredPlayerSourceType.BotByName],
                ],
                stringParam: "Frank\treached",
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();
        await Wired.OnRoomEventAsync(
            new Turbo.Primitives.Rooms.Events.Wired.RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = Turbo.Primitives.Action.ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(0, 11)],
            },
            Ct
        );
    }

    // Runs the room's 500 ms ticks and answers how many Chat the room has been sent so far.
    private async Task<int> TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 500;
            await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(_now, Ct);
            await _room.Harness.Room.BotTickSystem.ProcessBotsAsync(_now, Ct);
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }

        return _room.Harness.Fakes.SentToRoom<ChatMessageComposer>().Count();
    }

    // action.27.R3 (whisper: the user only) and action.27.R3b (talk: "results!: T2R" to everyone).
    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    public async Task Bot_talk_to_avatar_whispers_to_the_user_or_talks_with_the_users_name(
        int whisper
    )
    {
        AddBots();
        _room.Enter(USER, 5, 5, name: "results!");
        var talk = await SaveBotBoxAsync<WiredActionBotTalkToAvatar>(
            "wf_act_bot_talk_to_avatar",
            "Frank\tT2R",
            ints: [whisper, -1, 0],
            withUsers: true
        );

        await talk.ExecuteAsync(Execution(USER), Ct);

        var said = _room.Harness.Fakes.SentToRoom<ChatMessageComposer>().ToList();

        // Bots speak in bubble 31: every bot Chat, Shout and Whisper of the W8 labs (2026-10-24,
        // 100+ packets from 13 bot indexes) and the bots' placement greetings. Ours sent 2.
        if (whisper == 1)
            said.Should().BeEmpty();
        else
            said.Should()
                .ContainSingle()
                .Which.Should()
                .Match<ChatMessageComposer>(x => x.Text == "results!: T2R" && x.StyleId == 31);
    }

    // action.26.R3: a female figure saved for a male bot; Open returned the colours swapped
    // (hr-515-33 -> hr-515-40, ch-635-70 -> ch-635-1408), the pieces kept.
    [Fact]
    public async Task Bot_change_figure_stores_the_figure_with_its_colours_made_fit()
    {
        _room.Harness.Fakes.Handlers["GetAsync"] = call =>
            call.Interface == typeof(IFigureDataProvider)
                ? Task.FromResult(Figures())
                : Fakes.NotHandled;
        var clothes = await SaveBotBoxAsync<WiredActionBotChangeFigure>(
            "wf_act_bot_clothes",
            "frank\thr-515-33.ch-635-70.hd-600-1"
        );

        clothes.GetSnapshot().StringParam.Should().Be("frank\thr-515-40.ch-635-1408.hd-600-1");
    }

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    private WiredExecutionContext Execution(int? user = null)
    {
        var execution = new WiredExecutionContext(_room.Harness.Room)
        {
            CancellationToken = Ct,
            SelectorPool = new WiredSelectionSet(),
        };

        if (user is { } id)
            execution.SelectorPool.SelectedAvatarIds.Add(id);

        return execution;
    }

    private async Task<T> SaveBotBoxAsync<T>(
        string logicName,
        string text,
        int[]? ints = null,
        int[]? stuffIds = null,
        bool withUsers = false
    )
        where T : FurnitureWiredLogic
    {
        var box = _room.AddBox<T>(BOX, 0, 11, logicName);
        WiredPlayerSourceType[][] sources = withUsers
            ?
            [
                [WiredPlayerSourceType.BotByName],
                [WiredPlayerSourceType.SelectorUsers],
            ]
            :
            [
                [WiredPlayerSourceType.BotByName],
            ];

        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                intParams: ints ?? [],
                stuffIds: stuffIds,
                furniSources: stuffIds is null
                    ? null
                    :
                    [
                        [WiredFurniSourceType.SelectedItems],
                    ],
                playerSources: sources,
                stringParam: text,
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        return box;
    }

    private (RoomBotAvatar Frank, RoomBotAvatar FrankLower) AddBots() =>
        (AddBot(FRANK, "Frank", 1, 1), AddBot(FRANK_LOWER, "frank", 2, 1));

    private RoomBotAvatar AddBot(int id, string name, int x, int y)
    {
        var bot = new RoomBotAvatar
        {
            ObjectId = id,
            BotId = id,
            OwnerId = 1,
            OwnerName = "owner",
            RoomId = 1,
            Skills = [],
        };

        bot.SetName(name);
        bot.SetPosition(x, y);
        bot.SetLogic(_room.Harness.Fakes.Create<IRoomBotLogic>(id));
        _room.Harness.State.AvatarsByObjectId[id] = bot;
        _room.Harness.State.Bots.Add(bot);
        _room.Map.AddAvatar(bot, false);

        return bot;
    }

    // Hair (palette 1) and clothing (palette 3) with one club colour each, as the hotel's figure
    // data marks the colours the capture saw swapped.
    private static FigureData Figures()
    {
        static FigureSetType Kind(string type, int palette, int set) =>
            new(
                type,
                palette,
                false,
                false,
                false,
                false,
                new Dictionary<int, FigureSet>
                {
                    [set] = new(set, 'F', 0, true, true, true, false, 2),
                }
            );

        return new FigureData(
            new Dictionary<string, FigureSetType>
            {
                ["hr"] = Kind("hr", 1, 515),
                ["ch"] = Kind("ch", 3, 635),
            },
            new Dictionary<int, FigurePalette>
            {
                [1] = new(
                    1,
                    new Dictionary<int, FigureColor>
                    {
                        [40] = new(40, 1, 0, true),
                        [33] = new(33, 2, 2, true),
                    }
                ),
                [3] = new(
                    3,
                    new Dictionary<int, FigureColor>
                    {
                        [1408] = new(1408, 1, 0, true),
                        [70] = new(70, 2, 2, true),
                    }
                ),
            }
        );
    }
}
