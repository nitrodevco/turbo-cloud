using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Stacks fired with nobody behind them, and stacks holding two add-ons of one kind, as Habbo
/// runs them (wired matrix W1/W3/W6, official client captures 2026-10-24, the room owner alone in
/// their room). Under Periodically a box reading the triggering user has nobody to read: every
/// user condition passes over the empty set ("all" of nobody), its "not" twin is the exact
/// inverse, and the user effects run and change nothing. Two Animation Times add up, two
/// Movement Physics and two Projectiles keep the lower box, two Curves the upper one, two Carry
/// Users carry the user once, and the movement add-ons on a stack that moves nothing let it run.
/// Each test is named by the matrix case it holds.
/// </summary>
public sealed class WiredNoTriggererTests
{
    private const int CLICK_ME = 40;
    private const int FLOOR = 41;
    private const int BLOCK = 42;
    private const int USER = 5;
    private const int RIDER = 6;
    private const int PLAYER = 105;
    private const int MOVE_WEST = 7;
    private const int TEAM_RED_EFFECT = 223;

    private readonly WiredRoom _room = new(10, 10);
    private readonly List<int> _stackIds = [];
    private long _now = 10_000;
    private int _nextBoxId = 10;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    private RoomGameSystem Game => _room.Harness.Module<RoomGameSystem>();

    public WiredNoTriggererTests()
    {
        // No group in the room, nothing in hand, standing, not waving, alone.
        _room.Enter(USER, 5, 5);
        _room.AddFloorItem(CLICK_ME, 9, 9);
        _room.AddFloorItem(FLOOR, 2, 2);
    }

    /// <summary>
    /// condition.10/25/26/32/37/5 R2 and R3: under Periodically each passes, also where a click
    /// by the same user fails it (in group, hand item 2, waving); the "not" twins are the inverse
    /// both ways (10-59-48 periodic 3/3-4/4 each, 11-01-50 click 1/1 each).
    /// </summary>
    [Theory]
    [InlineData("wf_cnd_actor_in_group", true, false)]
    [InlineData("wf_cnd_not_in_group", false, true)]
    [InlineData("wf_cnd_has_handitem", true, false)]
    [InlineData("wf_cnd_not_has_handitem", false, true)]
    [InlineData("wf_cnd_triggerer_match", true, true)]
    [InlineData("wf_cnd_not_triggerer_match", false, false)]
    [InlineData("wf_cnd_user_performs_action", true, false)]
    [InlineData("wf_cnd_not_user_performs_action", false, true)]
    [InlineData("wf_cnd_actor_dir", true, true)]
    [InlineData("wf_cnd_user_count_in", true, true)]
    [InlineData("wf_cnd_not_user_count", false, false)]
    public async Task A_user_condition_passes_with_nobody_and_its_twin_is_the_inverse_condition_R2_R3(
        string logic,
        bool passesPeriodic,
        bool passesClick
    )
    {
        await BuildStackAsync(0, 0, periodic: true, then: id => AddConditionAsync(logic, id));
        await BuildStackAsync(0, 1, periodic: false, then: id => AddConditionAsync(logic, id));
        await StartAsync();

        await TickAsync(3);
        var periodic = Logged("ran 0,0");

        await ClickAsync();
        await TickAsync(1);

        (periodic > 0).Should().Be(passesPeriodic);
        (Logged("ran 0,1") > 0).Should().Be(passesClick);
    }

    /// <summary>
    /// action.19/31/32/42/43/44/8 R2 and action.9/10 R2: under Periodically the user effects run
    /// (the stack goes on) and nobody is kicked, frozen, moved, turned, sent away or put in or
    /// out of a team; nothing is sent about the user (10-59-48, 11-08-31, 3/3 each).
    /// </summary>
    [Theory]
    [InlineData("wf_act_kick_user")]
    [InlineData("wf_act_freeze")]
    [InlineData("wf_act_unfreeze")]
    [InlineData("wf_act_move_rotate_user")]
    [InlineData("wf_act_user_to_furni")]
    [InlineData("wf_act_teleport_to_room")]
    [InlineData("wf_act_teleport_to")]
    [InlineData("wf_act_join_team")]
    [InlineData("wf_act_leave_team")]
    public async Task A_user_effect_runs_and_does_nothing_with_nobody_action_R2(string logic)
    {
        var user = _room.Avatars[USER];

        if (logic == "wf_act_leave_team")
            await Game.JoinTeamAsync((PlayerId)PLAYER, GameTeamType.Red, Ct);

        var team = Game.GetTeam((PlayerId)PLAYER);
        var effect = user.EffectId;

        await BuildStackAsync(0, 0, periodic: true, then: id => AddEffectAsync(logic, id));
        await StartAsync();

        var before = _room.Harness.Fakes.Sent().Count();

        await TickAsync(3);

        Logged("ran 0,0").Should().BeGreaterThan(0);
        _room.Harness.State.AvatarsByObjectId.Should().ContainKey(USER);
        (user.X, user.Y, (int)user.Rotation).Should().Be((5, 5, 0));
        user.IsFrozen.Should().BeFalse();
        user.EffectId.Should().Be(effect);
        Game.GetTeam((PlayerId)PLAYER).Should().Be(team);
        _room
            .Harness.Fakes.Sent()
            .Skip(before)
            .Where(x =>
                x
                    is AvatarEffectMessageComposer
                        or WiredMovementsMessageComposer
                        or RoomForwardMessageComposer
                        or UserRemoveMessageComposer
            )
            .Should()
            .BeEmpty();
    }

    /// <summary>action.9.R2/R3: the same Join Team on a click puts the clicker in red, effect 223.</summary>
    [Fact]
    public async Task Join_team_on_a_click_joins_the_clicker_action_9_R3()
    {
        await BuildStackAsync(
            0,
            0,
            periodic: false,
            then: id => AddEffectAsync("wf_act_join_team", id)
        );
        await StartAsync();

        await ClickAsync();
        await TickAsync(1);

        Game.GetTeam((PlayerId)PLAYER).Should().Be(GameTeamType.Red);
        _room.Avatars[USER].EffectId.Should().Be(TEAM_RED_EFFECT);
    }

    /// <summary>
    /// addon.9.R2: Animation Time 175 and Animation Time 2000 on one stack move in 2175 ms, the
    /// two added up (11-04-38, 1/1; each alone 175 / 2000).
    /// </summary>
    [Fact]
    public async Task Two_animation_times_add_up_addon_9_R2()
    {
        await BuildMoverAsync(AnimationTime(175), AnimationTime(2000));

        var move = await ClickForMoveAsync();

        move.FloorItems.Should().ContainSingle().Which.AnimationTime.Should().Be(2175);
    }

    /// <summary>A hotel that keeps the upper box of a kind gets the old rule back.</summary>
    [Fact]
    public async Task Two_animation_times_keep_the_upper_box_when_the_hotel_says_so()
    {
        _room.Harness.Room.UseWiredConfig(new WiredConfig { DoubledAddonsAsMeasured = false });
        await BuildMoverAsync(AnimationTime(175), AnimationTime(2000));

        var move = await ClickForMoveAsync();

        move.FloorItems.Should().ContainSingle().Which.AnimationTime.Should().Be(2000);
    }

    /// <summary>
    /// addon.7.R2: Movement Physics with keep altitude below one without it: a furni moved off
    /// its height keeps it (11-04-38, 1/1; keep alone keeps it, the other alone drops it).
    /// </summary>
    [Fact]
    public async Task Two_physics_keep_the_lower_box_addon_7_R2()
    {
        await BuildMoverAsync(Physics(keepAltitude: true), Physics(keepAltitude: false));
        _room.FloorItem(BLOCK).SetPositionZ(Altitude.FromInt(205));

        await ClickForMoveAsync();

        _room.FloorItem(BLOCK).Z.Should().Be(Altitude.FromInt(205));
    }

    /// <summary>
    /// addon.21.R2: two Projectiles, the lower one curve 50 and the upper one 300: the move
    /// curves 50, the lower box (11-21-39, 1/1; each alone 50 / 300).
    /// </summary>
    [Fact]
    public async Task Two_projectiles_keep_the_lower_box_addon_21_R2()
    {
        await BuildMoverAsync(Projectile(50), Projectile(300));

        var move = await ClickForMoveAsync();

        move.FloorItems.Should().ContainSingle().Which.CurveStrength.Should().Be(50);
    }

    /// <summary>addon.22.R2: two Curves, 160 below and 500 above: the move curves 500 (11-04-38, 1/1).</summary>
    [Fact]
    public async Task Two_curves_keep_the_upper_box_addon_22_R2()
    {
        await BuildMoverAsync(Curve(160), Curve(500));

        var move = await ClickForMoveAsync();

        move.FloorItems.Should().ContainSingle().Which.CurveStrength.Should().Be(500);
    }

    /// <summary>addon.8.R2: two Carry Users with the user on the furni: one user move, as with one (11-04-38, 2/2).</summary>
    [Fact]
    public async Task Two_carry_users_carry_the_user_once_addon_8_R2()
    {
        var rider = _room.Enter(RIDER, 3, 3);
        await _room.Map.InvokeAvatarAsync(rider, Ct);
        _room.Clock.AdvanceMs(500);
        await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(_room.Harness.NowMs(), Ct);
        await BuildMoverAsync(Carry(), Carry());

        var move = await ClickForMoveAsync();

        move.Users.Should().ContainSingle().Which.TargetX.Should().Be(2);
    }

    /// <summary>
    /// addon.17.R2: two Execute In Order run the effects as one does, the lower box first:
    /// "B" below "A" said B then A each time (11-21-39, 4/4; one add-on 6/6).
    /// </summary>
    [Fact]
    public async Task Two_execute_in_order_run_the_lower_effect_first_addon_17_R2()
    {
        await BuildMoverAsync(ExecuteInOrder(), ExecuteInOrder());
        await AddLogAsync(7, "say B");
        await AddLogAsync(8, "say A");
        await StartAsync();

        for (var i = 0; i < 4; i++)
        {
            await ClickAsync();
            await TickAsync(1);
        }

        var said = Wired
            .GetRoomLogPage(1, 10_000, -1, -1, "say ")
            .Entries.Reverse()
            .Select(x => x.Message.Contains("say B") ? "B" : "A");

        said.Should().Equal("B", "A", "B", "A", "B", "A", "B", "A");
    }

    /// <summary>
    /// addon.9/6/7/8/22 R3: a movement add-on on a stack that moves nothing: the stack runs as
    /// without it, no error (11-06-13, 1/1 each).
    /// </summary>
    [Theory]
    [InlineData("wf_xtra_anim_time")]
    [InlineData("wf_xtra_mov_no_animation")]
    [InlineData("wf_xtra_mov_physics")]
    [InlineData("wf_xtra_mov_carry_users")]
    [InlineData("wf_xtra_mov_curve")]
    public async Task A_movement_add_on_without_a_mover_lets_the_stack_run_addon_R3(string logic)
    {
        Func<int, Task> addon = logic switch
        {
            "wf_xtra_anim_time" => AnimationTime(175),
            "wf_xtra_mov_no_animation" => NoAnimation(),
            "wf_xtra_mov_physics" => Physics(keepAltitude: true),
            "wf_xtra_mov_carry_users" => Carry(),
            _ => Curve(160),
        };

        await BuildStackAsync(0, 0, periodic: false, then: addon);
        await StartAsync();

        await ClickAsync();
        await TickAsync(1);

        Logged("ran 0,0").Should().Be(1);
    }

    private async Task AddConditionAsync(string logic, int id)
    {
        int[] ints = [];
        var stringParam = "";
        WiredPlayerSourceType[][] users =
        [
            [WiredPlayerSourceType.TriggeredUser],
        ];

        switch (logic)
        {
            case "wf_cnd_actor_in_group":
                _room.AddBox<WiredConditionGroupMember>(id, 0, 0, logic);
                break;
            case "wf_cnd_not_in_group":
                _room.AddBox<WiredNegativeConditionGroupMember>(id, 0, 0, logic);
                break;
            case "wf_cnd_has_handitem":
                _room.AddBox<WiredConditionHabboHasHanditem>(id, 0, 0, logic);
                ints = [2];
                break;
            case "wf_cnd_not_has_handitem":
                _room.AddBox<WiredNegativeConditionHabboHasHanditem>(id, 0, 0, logic);
                ints = [2];
                break;
            case "wf_cnd_triggerer_match":
            case "wf_cnd_not_triggerer_match":
                if (logic == "wf_cnd_triggerer_match")
                    _room.AddBox<WiredConditionExecutorMatches>(id, 0, 0, logic);
                else
                    _room.AddBox<WiredNegativeConditionExecutorMatches>(id, 0, 0, logic);
                ints = [1];
                users =
                [
                    [WiredPlayerSourceType.TriggeredUser],
                    [WiredPlayerSourceType.UserByName],
                ];
                break;
            case "wf_cnd_user_performs_action":
                _room.AddBox<WiredConditionHabboPerformsAction>(id, 0, 0, logic);
                ints = [(int)WiredAvatarActionType.Wave];
                break;
            case "wf_cnd_not_user_performs_action":
                _room.AddBox<WiredNegativeConditionHabboPerformsAction>(id, 0, 0, logic);
                ints = [(int)WiredAvatarActionType.Wave];
                break;
            case "wf_cnd_actor_dir":
                _room.AddBox<WiredConditionEntityDirection>(id, 0, 0, logic);
                ints = [255];
                break;
            case "wf_cnd_user_count_in":
                _room.AddBox<WiredConditionRoomHabboCount>(id, 0, 0, logic);
                ints = [1, 50];
                users = [];
                break;
            case "wf_cnd_not_user_count":
                _room.AddBox<WiredNegativeConditionRoomHabboCount>(id, 0, 0, logic);
                ints = [1, 50];
                users = [];
                break;
        }

        // AddBox puts the box at 0,0; the stack's own tile comes from the caller.
        (
            await _room.SaveAsync<UpdateConditionMessage>(
                id,
                intParams: ints,
                stringParam: stringParam,
                playerSources: users,
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeTrue(logic);
    }

    private async Task AddEffectAsync(string logic, int id)
    {
        int[] ints = [];
        int[] picks = [];

        switch (logic)
        {
            case "wf_act_kick_user":
                _room.AddBox<WiredActionKickUser>(id, 0, 0, logic);
                break;
            case "wf_act_freeze":
                _room.AddBox<WiredActionFreezeUser>(id, 0, 0, logic);
                ints = [0, 1];
                break;
            case "wf_act_unfreeze":
                _room.AddBox<WiredActionUnfreezeUser>(id, 0, 0, logic);
                break;
            case "wf_act_move_rotate_user":
                _room.AddBox<WiredActionMoveRotateUser>(id, 0, 0, logic);
                ints = [1, -1];
                break;
            case "wf_act_user_to_furni":
                _room.AddBox<WiredActionUserToFurni>(id, 0, 0, logic);
                ints = [(int)WiredUserWalkModeType.KeepWalkingIfCloser];
                picks = [FLOOR];
                break;
            case "wf_act_teleport_to_room":
                _room.AddBox<WiredActionTeleportToRoom>(id, 0, 0, logic);
                break;
            case "wf_act_teleport_to":
                _room.AddBox<WiredActionTeleportTo>(id, 0, 0, logic);
                ints = [0];
                picks = [FLOOR];
                break;
            case "wf_act_join_team":
                _room.AddBox<WiredActionJoinTeam>(id, 0, 0, logic);
                ints = [(int)GameTeamType.Red, (int)WiredTeamType.Wired];
                break;
            case "wf_act_leave_team":
                _room.AddBox<WiredActionLeaveTeam>(id, 0, 0, logic);
                break;
        }

        (
            await _room.SaveWithResultAsync<UpdateActionMessage>(
                id,
                intParams: ints,
                stringParam: logic == "wf_act_kick_user" ? "bye" : "",
                stuffIds: picks,
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            )
        ).Should().Be(WiredSaveResult.Saved, logic);
    }

    private Func<int, Task> AnimationTime(int ms) =>
        async id =>
        {
            _room.AddBox<WiredAddonAnimationTime>(id, 0, 0, "wf_xtra_anim_time");
            (await _room.SaveAsync<UpdateAddonMessage>(id, intParams: [ms])).Should().BeTrue();
        };

    private Func<int, Task> ExecuteInOrder() =>
        async id =>
        {
            _room.AddBox<WiredAddonExecuteInOrder>(id, 0, 0, "wf_xtra_exec_in_order");
            (await _room.SaveAsync<UpdateAddonMessage>(id)).Should().BeTrue();
        };

    private async Task AddLogAsync(int id, string text)
    {
        _room.AddBox<WiredActionWriteToLogs>(id, 0, 0, "wf_act_log");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                id,
                intParams: [(int)WiredLogLevelType.Warning],
                stringParam: text,
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeTrue();
    }

    private Func<int, Task> NoAnimation() =>
        async id =>
        {
            _room.AddBox<WiredAddonCancelAnimationTime>(id, 0, 0, "wf_xtra_mov_no_animation");
            (await _room.SaveAsync<UpdateAddonMessage>(id)).Should().BeTrue();
        };

    private Func<int, Task> Physics(bool keepAltitude) =>
        async id =>
        {
            _room.AddBox<WiredAddonMovementPhysics>(id, 0, 0, "wf_xtra_mov_physics");
            (
                await _room.SaveAsync<UpdateAddonMessage>(
                    id,
                    intParams: [keepAltitude ? 1 : 0, 0, 0, 0],
                    furniSources:
                    [
                        [WiredFurniSourceType.AllRoomItems],
                        [WiredFurniSourceType.AllRoomItems],
                    ],
                    playerSources:
                    [
                        [WiredPlayerSourceType.AllRoomUsers],
                    ]
                )
            ).Should().BeTrue();
        };

    private Func<int, Task> Projectile(int curve) =>
        async id =>
        {
            _room.AddBox<WiredAddonProjectile>(id, 0, 0, "wf_xtra_rotate_to_dir");
            (
                await _room.SaveAsync<UpdateAddonMessage>(
                    id,
                    intParams: [1, 1, 0, 0, 100, -20, 1, 1, 0, 4, 0, 0, 1, 1, 0, 0, 1, -20, curve],
                    furniSources:
                    [
                        [WiredFurniSourceType.MovedItems],
                        [WiredFurniSourceType.TriggeredItem],
                        [WiredFurniSourceType.TriggeredItem],
                    ],
                    playerSources:
                    [
                        [WiredPlayerSourceType.TriggeredUser],
                        [WiredPlayerSourceType.TriggeredUser],
                        [WiredPlayerSourceType.TriggeredUser],
                    ]
                )
            ).Should().BeTrue();
        };

    private Func<int, Task> Curve(int strength) =>
        async id =>
        {
            _room.AddBox<WiredAddonJumpStrength>(id, 0, 0, "wf_xtra_mov_curve");
            (await _room.SaveAsync<UpdateAddonMessage>(id, intParams: [0, strength, -20]))
                .Should()
                .BeTrue();
        };

    private Func<int, Task> Carry() =>
        async id =>
        {
            _room.AddBox<WiredAddonCarryUsers>(id, 0, 0, "wf_xtra_mov_carry_users");
            (
                await _room.SaveAsync<UpdateAddonMessage>(
                    id,
                    intParams: [(int)WiredCarryUserType.StandingOnFurni],
                    playerSources:
                    [
                        [WiredPlayerSourceType.AllRoomUsers],
                    ]
                )
            ).Should().BeTrue();
        };

    /// <summary>
    /// A stack at <paramref name="x"/>, <paramref name="y"/>: Periodically (every half second) or
    /// a click on <see cref="CLICK_ME"/>, then the box <paramref name="then"/> adds, then "write
    /// to logs" saying "ran x,y" so a test sees the stack get past its conditions.
    /// </summary>
    private async Task BuildStackAsync(int x, int y, bool periodic, Func<int, Task> then)
    {
        var triggerId = _nextBoxId++;

        if (periodic)
        {
            _room.AddBox<WiredTriggerPeriodically>(triggerId, 0, 0, "wf_trg_periodically");
            (await _room.SaveAsync<UpdateTriggerMessage>(triggerId, intParams: [1]))
                .Should()
                .BeTrue();
        }
        else
        {
            _room.AddBox<WiredTriggerClickFurni>(triggerId, 0, 0, "wf_trg_click_furni");
            (
                await _room.SaveAsync<UpdateTriggerMessage>(
                    triggerId,
                    stuffIds: [CLICK_ME],
                    furniSources:
                    [
                        [WiredFurniSourceType.SelectedItems],
                    ]
                )
            ).Should().BeTrue();
        }

        var boxId = _nextBoxId++;
        await then(boxId);

        var logId = _nextBoxId++;
        _room.AddBox<WiredActionWriteToLogs>(logId, 0, 0, "wf_act_log");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                logId,
                intParams: [(int)WiredLogLevelType.Warning],
                stringParam: $"ran {x},{y}",
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeTrue();

        foreach (var id in new[] { triggerId, boxId, logId })
            MoveTo(id, x, y);

        _stackIds.Add(_room.Map.ToIdx(x, y));
    }

    /// <summary>
    /// A click moves the block at 3,3 one tile west, the two add-ons stacked in this order
    /// (the first one lower).
    /// </summary>
    private async Task BuildMoverAsync(Func<int, Task> lower, Func<int, Task> upper)
    {
        _room.AddFloorItem(BLOCK, 3, 3);
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                1,
                stuffIds: [CLICK_ME],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();

        _room.AddBox<WiredActionMoveRotateFurni>(2, 0, 0, "wf_act_move_rotate");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                2,
                intParams: [MOVE_WEST, 0],
                stuffIds: [BLOCK],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        await lower(3);
        await upper(4);

        _stackIds.Add(_room.Map.ToIdx(0, 0));
        await StartAsync();
    }

    private void MoveTo(int id, int x, int y)
    {
        if (x == 0 && y == 0)
            return;

        var item = _room.FloorItem(id);

        _room.Map.RemoveItem(item);
        item.SetPosition(x, y);
        _room.Map.AddItem(item);
    }

    private async Task<WiredMovementsMessageComposer> ClickForMoveAsync()
    {
        var before = _room.Harness.Fakes.SentToRoom<WiredMovementsMessageComposer>().Count();

        await ClickAsync();
        await TickAsync(3);

        return _room
            .Harness.Fakes.SentToRoom<WiredMovementsMessageComposer>()
            .Skip(before)
            .Single();
    }

    private async Task StartAsync()
    {
        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [.. _stackIds],
            },
            Ct
        );

        await TickAsync(1);
    }

    private Task ClickAsync() =>
        _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)PLAYER, (RoomId)1), 0, Ct);

    private int Logged(string text) => Wired.GetRoomLogPage(1, 10_000, -1, -1, text).TotalEntries;

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }
}
