using FluentAssertions;
using Microsoft.Extensions.Options;
using Turbo.Primitives.Action;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Avatars.Bot;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Providers;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Bots are users to wired (official client capture 2026-10-24, W1/W2/W3 lab with bots "Frank"
/// and "frank" in the owner's room; case ids from the W3 sheet). A bot's line, a bot placed, a bot
/// picked up and a bot starting to dance fire the user triggers, the bot triggering; Actor In
/// Group fails for a bot and User Count In Room does not count one; Chase chases a bot and Move
/// Furni To User moves onto one. Ours fired none of those triggers for a bot, fired Performs
/// Action when a dance stopped, let a bot triggering pass Actor In Group as if nobody had, and
/// moved furni for players only.
/// </summary>
public sealed class WiredBotsAsUsersTests
{
    private const int OWNER = 5;
    private const int FRANK = 20;
    private const int FRANK_LOWER = 21;
    private const int TRIGGER = 1;
    private const int EFFECT = 2;
    private const int CONDITION = 3;

    private readonly WiredRoom _room = new(12, 12);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    private RoomBotModule Bots => _room.Harness.Module<RoomBotModule>();

    public WiredBotsAsUsersTests() => _room.Enter(OWNER, 1, 1, "owner", playerId: 1);

    // trigger.0.R2 (7/7 fires, triggerer the bot 4/4).
    [Fact]
    public async Task A_bot_line_fires_says_keyword_with_the_bot_triggering()
    {
        var (frank, frankLower) = AddBots();
        await BuildAsync<WiredTriggerHabboSaysKeyword>("wf_trg_says_something", [0, 0, 0], "hello");

        await Bots.TalkAsync(frank, "Hello there", false, null, false, Ct);
        await TickAsync(2);

        frank.IsFrozen.Should().BeTrue();
        frankLower.IsFrozen.Should().BeFalse();
        _room.Avatars[OWNER].IsFrozen.Should().BeFalse();
    }

    // trigger.7.R2 (3/3 fires, triggerer the bot 1/1).
    [Fact]
    public async Task A_bot_placed_fires_enter_room_with_the_bot_triggering()
    {
        _room.Harness.Room.UseAvatarProvider(
            new RoomAvatarProvider(Options.Create(new BotConfig()))
        );
        _room.Harness.Fakes.Handlers[nameof(IInventoryGrain.TryCheckOutBotAsync)] = _ =>
            Task.FromResult<BotSnapshot?>(Snapshot(FRANK, "Frank"));
        await BuildAsync<WiredTriggerHabboJoinRoom>("wf_trg_enter_room", [], "");

        (await Bots.PlaceBotAsync(ActionContext.CreateForPlayer(1, 1), FRANK, 4, 4, Ct))
            .Should()
            .BeTrue();
        await TickAsync(2);

        _room.Harness.State.Bots.Should().ContainSingle(x => x.IsFrozen);
        _room.Avatars[OWNER].IsFrozen.Should().BeFalse();
    }

    // trigger.23.R2 (2/2 fires).
    [Fact]
    public async Task A_bot_picked_up_fires_leave_room()
    {
        AddBots();
        var trigger = await BuildAsync<WiredTriggerHabboLeaveRoom>("wf_trg_leave_room", [], "");
        var before = trigger.GetState();

        (await Bots.PickupBotAsync(ActionContext.CreateForPlayer(1, 1), FRANK, Ct))
            .Should()
            .BeTrue();
        await TickAsync(2);

        trigger.GetState().Should().NotBe(before);
    }

    // trigger.16.R2 (starting 3/3, stopping 0/2).
    [Fact]
    public async Task A_bot_starting_to_dance_fires_performs_action_and_stopping_does_not()
    {
        var (frank, _) = AddBots();
        var avatars = _room.Harness.Module<RoomAvatarModule>();
        await BuildAsync<WiredTriggerHabboPerformsAction>(
            "wf_trg_user_performs_action",
            [(int)WiredAvatarActionType.Dance],
            ""
        );

        await avatars.SetAvatarDanceAsync(frank.ObjectId, AvatarDanceType.PogoMogo, Ct);
        await TickAsync(2);
        frank.IsFrozen.Should().BeTrue();

        frank.SetFrozen(false);
        await avatars.SetAvatarDanceAsync(frank.ObjectId, AvatarDanceType.None, Ct);
        await TickAsync(2);
        frank.IsFrozen.Should().BeFalse();
    }

    // condition.10.B2 (Actor In Group fails 4/4 for a bot triggering) and condition.5.B2 (the
    // room holds the owner and two bots: [1,1] passes 4/4, [3,3] fails 4/4).
    [Theory]
    [InlineData("wf_cnd_actor_in_group", new int[0], false)]
    [InlineData("wf_cnd_user_count_in", new[] { 1, 1 }, true)]
    [InlineData("wf_cnd_user_count_in", new[] { 3, 3 }, false)]
    public async Task A_condition_asks_a_bot_triggering_what_it_asks_a_user(
        string condition,
        int[] ints,
        bool passes
    ) => (await BotTriggeringPassesAsync(condition, ints, "")).Should().Be(passes);

    // What only a player has: a bot wears no badge and is in no team, so it fails these as it
    // fails Actor In Group, rather than leaving the selection empty (which passes).
    [Theory]
    [Trait("evidence", "inferred")]
    [InlineData("wf_cnd_wearing_badge", new int[0], "ADM")]
    [InlineData("wf_cnd_actor_in_team", new[] { 0 }, "")]
    public async Task A_bot_triggering_has_no_badge_and_no_team(
        string condition,
        int[] ints,
        string text
    ) => (await BotTriggeringPassesAsync(condition, ints, text)).Should().BeFalse();

    /// <summary>Whether <paramref name="condition"/> lets a stack through when bot Frank talks.</summary>
    private async Task<bool> BotTriggeringPassesAsync(string condition, int[] ints, string text)
    {
        var (frank, _) = AddBots();
        await BuildAsync<WiredTriggerHabboSaysKeyword>("wf_trg_says_something", [0, 2, 0], "");
        _ = condition switch
        {
            "wf_cnd_actor_in_group" => (object)
                _room.AddBox<WiredConditionGroupMember>(CONDITION, 6, 6, condition),
            "wf_cnd_user_count_in" => _room.AddBox<WiredConditionRoomHabboCount>(
                CONDITION,
                6,
                6,
                condition
            ),
            "wf_cnd_wearing_badge" => _room.AddBox<WiredConditionHabboHasWearingBadge>(
                CONDITION,
                6,
                6,
                condition
            ),
            _ => _room.AddBox<WiredConditionHabboInTeam>(CONDITION, 6, 6, condition),
        };
        (
            await _room.SaveAsync<UpdateConditionMessage>(
                CONDITION,
                intParams: ints,
                stringParam: text
            )
        )
            .Should()
            .BeTrue();
        await StartAsync();

        await Bots.TalkAsync(frank, "hi", false, null, false, Ct);
        await TickAsync(2);

        return frank.IsFrozen;
    }

    // trigger.0.R2: the probe "P T=$(n)" with a username placeholder on the triggering user,
    // whispered to the owner picked by name, printed the bot's name ("P T=Frank").
    [Fact]
    public async Task The_username_placeholder_prints_a_bot_triggering()
    {
        var (frank, _) = AddBots();
        _room.AddBox<WiredTriggerHabboSaysKeyword>(TRIGGER, 6, 6, "wf_trg_says_something");
        _room.AddBox<WiredSelectorEntitiesByName>(7, 6, 6, "wf_slc_users_byname");
        _room.AddBox<WiredAddonUsernamePlaceholder>(8, 6, 6, "wf_xtra_text_output_username");
        _room.AddBox<WiredActionShowMessage>(9, 6, 6, "wf_act_show_message");
        (await _room.SaveAsync<UpdateTriggerMessage>(TRIGGER, intParams: [0, 2, 0]))
            .Should()
            .BeTrue();
        (
            await _room.SaveAsync<UpdateSelectorMessage>(
                7,
                stringParam: "owner",
                definitionSpecifics: [false, false]
            )
        )
            .Should()
            .BeTrue();
        (
            await _room.SaveAsync<UpdateAddonMessage>(
                8,
                intParams: [0],
                stringParam: "n",
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ]
            )
        ).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                9,
                intParams: [0, 34, -1],
                stringParam: "P T=$(n)",
                playerSources:
                [
                    [WiredPlayerSourceType.SelectorUsers],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();
        await StartAsync();

        await Bots.TalkAsync(frank, "hi", false, null, false, Ct);
        await TickAsync(2);

        _room
            .Harness.Fakes.SentToPlayer<ChatMessageComposer>(1)
            .Select(x => x.Text)
            .Should()
            .Contain("P T=Frank");
    }

    // W1 lab action.11.B3: the only avatar in line within 3 tiles was a bot; the block stepped
    // toward it every time (3/3, a random step would be 1/4).
    [Fact]
    public async Task Chase_chases_a_bot()
    {
        AddBot(FRANK, "Frank", 8, 9);
        var chase = await PlaceMoverAsync<WiredActionChaseHabbo>("wf_act_chase", 8, 6);

        for (var i = 0; i < 8; i++)
        {
            _room.Map.MoveFloorItem(_room.FloorItem(BLOCK), _room.Map.ToIdx(8, 6));

            await chase.ExecuteAsync(MoverContext(), Ct);

            Block().Should().Be((8, 7));
        }
    }

    // Flee flees a bot as Chase chases one (only Chase was recorded with a bot).
    [Fact]
    [Trait("evidence", "inferred")]
    public async Task Flee_flees_a_bot()
    {
        AddBot(FRANK, "Frank", 8, 8);
        var flee = await PlaceMoverAsync<WiredActionFleeHabbo>("wf_act_flee", 8, 6);

        await flee.ExecuteAsync(MoverContext(), Ct);

        Block().Should().Be((8, 5));
    }

    // W1 lab action.35.B3: Move Furni To User on bot "frank" moved the block onto its tile.
    [Fact]
    public async Task Move_furni_to_user_moves_onto_a_bot()
    {
        AddBot(FRANK, "Frank", 8, 9);
        var toUser = await PlaceMoverAsync<WiredActionFurniToUser>("wf_act_furni_to_user", 6, 7);
        var ctx = MoverContext();
        ctx.Selected.SelectedAvatarIds.Add(FRANK);

        await toUser.ExecuteAsync(ctx, Ct);

        Block().Should().Be((8, 9));
    }

    private const int BLOCK = 30;
    private const int MOVER = 10;

    private (int, int) Block() => (_room.FloorItem(BLOCK).X, _room.FloorItem(BLOCK).Y);

    private WiredExecutionContext MoverContext() =>
        new(_room.Harness.Room) { CancellationToken = Ct };

    private async Task<T> PlaceMoverAsync<T>(string logicName, int x, int y)
        where T : FurnitureWiredMoveActionLogic
    {
        _room.AddFloorItem(BLOCK, x, y);

        var box = _room.AddBox<T>(MOVER, 0, 11, logicName);

        (
            await _room.SaveAsync<UpdateActionMessage>(
                MOVER,
                stuffIds: [BLOCK],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                playerSources: typeof(T) == typeof(WiredActionFurniToUser)
                    ?
                    [
                        [WiredPlayerSourceType.TriggeredUser],
                    ]
                    : null,
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        return box;
    }

    /// <summary>The trigger under test and a Freeze on the triggering user, on tile 6,6.</summary>
    private async Task<T> BuildAsync<T>(string logic, int[] ints, string text)
        where T : FurnitureWiredTriggerLogic
    {
        var trigger = _room.AddBox<T>(TRIGGER, 6, 6, logic);
        _room.AddBox<WiredActionFreezeUser>(EFFECT, 6, 6, "wf_act_freeze");

        (await _room.SaveAsync<UpdateTriggerMessage>(TRIGGER, intParams: ints, stringParam: text))
            .Should()
            .BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                EFFECT,
                intParams: [0, 0],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();
        await StartAsync();

        return trigger;
    }

    private async Task StartAsync()
    {
        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(6, 6)],
            },
            Ct
        );
        await TickAsync(1);
    }

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }

    private (RoomBotAvatar Frank, RoomBotAvatar FrankLower) AddBots() =>
        (AddBot(FRANK, "Frank", 3, 3), AddBot(FRANK_LOWER, "frank", 4, 3));

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
        _room.Harness.State.AvatarsByBotId[id] = id;
        _room.Harness.State.Bots.Add(bot);
        _room.Map.AddAvatar(bot, false);

        return bot;
    }

    private static BotSnapshot Snapshot(int id, string name) =>
        new()
        {
            Id = id,
            OwnerId = 1,
            OwnerName = "owner",
            RoomId = null,
            Name = name,
            Motto = "",
            Figure = "hr-100.hd-180-1",
            Gender = AvatarGenderType.Male,
            X = 0,
            Y = 0,
            Z = 0,
            Rotation = Rotation.South,
            FreeRoam = false,
            ChatText = "",
            AutoChat = false,
            ChatDelaySeconds = 30,
            MixSentences = false,
            DanceType = AvatarDanceType.None,
        };
}
