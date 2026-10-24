using FluentAssertions;
using Turbo.Primitives.Figures;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Bot;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Avatars.Bot;
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

        if (whisper == 1)
            said.Should().BeEmpty();
        else
            said.Should().ContainSingle().Which.Text.Should().Be("results!: T2R");
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
