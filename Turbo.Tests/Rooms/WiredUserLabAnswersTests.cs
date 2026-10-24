using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The user boxes as they were seen on the official hotel (wired matrix W3, official client
/// captures 2026-10-24, the room owner alone in their room, a furni click as the trigger): each
/// test is named by the matrix case it holds. Move User turns an eighth at a time and cannot turn
/// a sitter; Has Hand Item 0 is an empty hand; Kick keeps the owner and tells them so; a frozen
/// user teleported without "cancel on teleport" gets the freeze effect back; Show Message speaks
/// from the triggerer only, and nothing without one.
/// </summary>
public sealed class WiredUserLabAnswersTests
{
    private const int BOX = 2;
    private const int TARGET = 30;
    private const int USER = 5;
    private const int OWNER_PLAYER = 1;
    private const long FIRED_AT = 10_000;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    /// <summary>action.42.E10: rotation 9 turns clockwise 4 -> 5 -> 6 -> 7 -> 0, 10 back 0 -> 7 -> 6 -> 5 -> 4.</summary>
    [Theory]
    [InlineData(9, 4, new[] { 5, 6, 7, 0 })]
    [InlineData(10, 0, new[] { 7, 6, 5, 4 })]
    public async Task Move_user_turns_an_eighth_per_fire_action_42_E10(
        int rotation,
        int facing,
        int[] expected
    )
    {
        var user = _room.Enter(USER, 3, 3, rotation: (Rotation)facing);
        var box = await PlaceMoveUserAsync(rotation);

        var seen = new List<int>();

        for (var i = 0; i < expected.Length; i++)
        {
            await ExecuteAsync(box, USER);
            seen.Add((int)user.Rotation);
        }

        seen.Should().Equal(expected);
    }

    /// <summary>
    /// action.42.B3: on a sitter facing 4, rotation 9 announces body 5, but the seat keeps the
    /// user facing 4, so the next fire announces 5 again.
    /// </summary>
    [Fact]
    public async Task Move_user_announces_a_turn_on_a_sitter_but_the_seat_holds_it_action_42_B3()
    {
        var user = _room.Enter(USER, 3, 3, rotation: (Rotation)4);
        user.Sit(true, rot: (Rotation)4);
        var box = await PlaceMoveUserAsync(9);

        var first = await ExecuteAsync(box, USER);
        var second = await ExecuteAsync(box, USER);

        first.UserDirections.Should().ContainSingle().Which.BodyRotation.Should().Be((Rotation)5);
        second.UserDirections.Should().ContainSingle().Which.BodyRotation.Should().Be((Rotation)5);
        user.Rotation.Should().Be((Rotation)4);
    }

    /// <summary>condition.25.E9: hand item 0 holds for an empty hand and not for one holding item 4.</summary>
    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(0, 4, false)]
    [InlineData(4, 4, true)]
    [InlineData(4, 0, false)]
    [InlineData(2, 4, false)]
    public async Task Has_hand_item_zero_is_an_empty_hand_condition_25_E9(
        int saved,
        int holding,
        bool holds
    )
    {
        _room.Enter(USER, 1, 1).SetHandItem(holding);
        var condition = _room.AddBox<WiredConditionHabboHasHanditem>(
            BOX,
            0,
            0,
            "wf_cnd_has_handitem"
        );

        (
            await _room.SaveAsync<UpdateConditionMessage>(
                BOX,
                intParams: [saved],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ]
            )
        ).Should().BeTrue();

        condition.Evaluate(TriggeredBy(USER)).Should().Be(holds);
    }

    /// <summary>
    /// condition.32.B3: Performs Action "stand", "lay" and "sit" hold for the posture the user is
    /// in, not only at the moment they took it: a lying user passes lay and fails stand, a
    /// standing one passes stand and fails sit (official client capture 2026-10-24T17-58-01 /
    /// 17-59-15, WIN63-202610201032, 4/4).
    /// </summary>
    [Theory]
    [InlineData(true, WiredAvatarActionType.Lay, true)]
    [InlineData(true, WiredAvatarActionType.Stand, false)]
    [InlineData(false, WiredAvatarActionType.Stand, true)]
    [InlineData(false, WiredAvatarActionType.Sit, false)]
    public async Task Performs_action_reads_the_current_posture_condition_32_B3(
        bool lying,
        WiredAvatarActionType action,
        bool holds
    )
    {
        var user = _room.Enter(USER, 1, 1);

        if (lying)
            user.AddStatus(AvatarStatusType.Lay, "0.5");

        var condition = _room.AddBox<WiredConditionHabboPerformsAction>(
            BOX,
            0,
            0,
            "wf_cnd_user_performs_action"
        );

        (
            await _room.SaveAsync<UpdateConditionMessage>(
                BOX,
                intParams: [(int)action],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ]
            )
        ).Should().BeTrue();

        condition.Evaluate(TriggeredBy(USER)).Should().Be(holds);
    }

    /// <summary>
    /// action.19.B1: Kick on the room owner does not kick them; they are whispered "Wired kick
    /// exception: Room owner" in bubble 34 over their own avatar, each fire.
    /// </summary>
    [Fact]
    public async Task Kick_keeps_the_owner_and_whispers_the_exception_action_19_B1()
    {
        _room.Enter(USER, 1, 1, playerId: OWNER_PLAYER);
        var box = _room.AddBox<WiredActionKickUser>(BOX, 0, 0, "wf_act_kick_user");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                stringParam: "bye",
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        (await box.ExecuteAsync(Triggered(USER), Ct)).Should().BeFalse();
        await box.ExecuteAsync(Triggered(USER), Ct);

        _room.Positions().Should().Equal($"{USER}@1,1");
        var whispers = _room
            .Harness.Fakes.SentToPlayer<WhisperMessageComposer>(OWNER_PLAYER)
            .ToList();
        whispers.Should().HaveCount(2);
        whispers
            .Should()
            .AllSatisfy(w =>
            {
                w.Text.Should().Be("Wired kick exception: Room owner");
                w.StyleId.Should().Be(34);
                w.ObjectId.Should().Be((RoomObjectId)USER);
                w.ReceiverRoomIndex.Should().Be(USER);
            });
    }

    /// <summary>action.31.B1: Freeze on the room owner freezes them, with effect 218.</summary>
    [Fact]
    public async Task Freeze_is_not_kept_from_the_owner_action_31_B1()
    {
        var owner = _room.Enter(USER, 1, 1, playerId: OWNER_PLAYER);
        var freeze = await PlaceFreezeAsync(cancelOnTeleport: false);

        await freeze.ExecuteAsync(Triggered(USER), Ct);

        owner.IsFrozen.Should().BeTrue();
        owner.EffectId.Should().Be(218);
    }

    /// <summary>
    /// action.31.R7: frozen, then teleported. Without "cancel on teleport" the user lands still
    /// frozen and wears 218 again when the teleport effect ends; with it the user lands thawed
    /// and the effect goes.
    /// </summary>
    [Theory]
    [InlineData(false, true, 218)]
    [InlineData(true, false, 0)]
    public async Task A_frozen_user_teleported_gets_the_freeze_effect_back_unless_it_cancels_action_31_R7(
        bool cancelOnTeleport,
        bool frozenAfter,
        int effectAfter
    )
    {
        var user = _room.Enter(USER, 1, 1);
        _room.AddFloorItem(TARGET, 5, 5);
        var freeze = await PlaceFreezeAsync(cancelOnTeleport);
        var teleport = await PlaceTeleportAsync(WiredFurniSourceType.SelectedItems);
        await Wired.ProcessWiredAsync(FIRED_AT, dormant: false, Ct);

        await freeze.ExecuteAsync(Triggered(USER), Ct);
        (await teleport.ExecuteAsync(Triggered(USER), Ct)).Should().BeTrue();
        user.EffectId.Should().Be(4);

        await Wired.ProcessWiredAsync(FIRED_AT + 2_000, dormant: false, Ct);
        _room.Positions().Should().Equal($"{USER}@5,5");

        await Wired.ProcessWiredAsync(FIRED_AT + 3_500, dormant: false, Ct);
        user.IsFrozen.Should().Be(frozenAfter);
        user.EffectId.Should().Be(effectAfter);
    }

    /// <summary>action.8.B3: a sitter teleported stands on the target.</summary>
    [Fact]
    public async Task A_sitter_teleported_stands_on_the_target_action_8_B3()
    {
        var user = _room.Enter(USER, 1, 1);
        user.Sit(true);
        _room.AddFloorItem(TARGET, 5, 5);
        var teleport = await PlaceTeleportAsync(WiredFurniSourceType.SelectedItems);
        await Wired.ProcessWiredAsync(FIRED_AT, dormant: false, Ct);

        (await teleport.ExecuteAsync(Triggered(USER), Ct)).Should().BeTrue();
        await Wired.ProcessWiredAsync(FIRED_AT + 2_000, dormant: false, Ct);
        // The room tick invokes an avatar that was put down: its seat, or none, decides its posture.
        await _room.Map.InvokeAvatarAsync(user, Ct);

        _room.Positions().Should().Equal($"{USER}@5,5");
        user.HasStatus(AvatarStatusType.Sit).Should().BeFalse();
    }

    /// <summary>action.8.R1: Teleport To on a selector source with no selector is a no-op.</summary>
    [Fact]
    public async Task Teleport_to_an_empty_selection_does_nothing_action_8_R1()
    {
        var user = _room.Enter(USER, 1, 1);
        _room.AddFloorItem(TARGET, 5, 5);
        var teleport = await PlaceTeleportAsync(WiredFurniSourceType.SelectorItems);

        (await teleport.ExecuteAsync(Triggered(USER), Ct)).Should().BeFalse();

        user.EffectId.Should().Be(0);
        _room.Positions().Should().Equal($"{USER}@1,1");
    }

    /// <summary>action.43.R1: User To Furni on a selector source with no selector is a no-op.</summary>
    [Fact]
    public async Task User_to_furni_with_an_empty_selection_does_nothing_action_43_R1()
    {
        _room.Enter(USER, 1, 1);
        _room.AddFloorItem(TARGET, 2, 2);
        var box = _room.AddBox<WiredActionUserToFurni>(BOX, 0, 0, "wf_act_user_to_furni");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                intParams: [0],
                furniSources:
                [
                    [WiredFurniSourceType.SelectorItems],
                ],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        var ctx = Triggered(USER);
        (await box.ExecuteAsync(ctx, Ct)).Should().BeFalse();

        ctx.UserMoves.Should().BeEmpty();
        _room.Positions().Should().Equal($"{USER}@1,1");
    }

    /// <summary>
    /// action.7.R3: visibility 0 is a whisper from the triggerer to the triggerer, 1 a chat from
    /// the triggerer, both in the saved style and width.
    /// </summary>
    [Fact]
    public async Task Show_message_whispers_or_says_it_from_the_triggerer_action_7_R3()
    {
        _room.Enter(USER, 1, 1);

        var whisper = await PlaceShowMessageAsync(BOX, visibility: 0, "one");
        await whisper.ExecuteAsync(Triggered(USER), Ct);
        var said = await PlaceShowMessageAsync(BOX + 1, visibility: 1, "two");
        await said.ExecuteAsync(Triggered(USER), Ct);

        var w = _room.Harness.Fakes.SentToPlayer<WhisperMessageComposer>(100 + USER).Single();
        (w.Text, w.ObjectId, w.ReceiverRoomIndex, w.StyleId)
            .Should()
            .Be(("one", (RoomObjectId)USER, (int?)USER, 202));
        var c = _room
            .Harness.Fakes.SentToRoom<ChatMessageComposer>()
            .Single(x => x.GetType() == typeof(ChatMessageComposer));
        (c.Text, c.ObjectId, c.StyleId).Should().Be(("two", (RoomObjectId)USER, 202));
    }

    /// <summary>
    /// action.7.R2 / action.7.E10: with no triggerer (a periodic fire) nothing is said, for
    /// either visibility; a box never saved (no text) says nothing either.
    /// </summary>
    [Theory]
    [InlineData(0, "hello", false)]
    [InlineData(1, "hello", false)]
    [InlineData(0, "", true)]
    public async Task Show_message_says_nothing_without_a_triggerer_or_text_action_7_R2(
        int visibility,
        string text,
        bool triggered
    )
    {
        _room.Enter(USER, 1, 1);
        var box = await PlaceShowMessageAsync(BOX, visibility, text);

        var ctx = triggered
            ? Triggered(USER)
            : new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };
        (await box.ExecuteAsync(ctx, Ct)).Should().BeFalse();

        _room.Harness.Fakes.Sent<ChatMessageComposer>().Should().BeEmpty();
    }

    /// <summary>
    /// action.7.X2: a placeholder is filled by its exact name; an unknown name and the right name
    /// in other letters stay as written ("u=$(un) x=$(nope) y=$(UN)" -> "u=Hutch. x=$(nope) y=$(UN)").
    /// </summary>
    [Fact]
    public async Task Placeholders_match_their_exact_name_only_action_7_X2()
    {
        _room.Enter(USER, 1, 1, "Hutch.");
        var placeholder = _room.AddBox<WiredAddonUsernamePlaceholder>(
            BOX,
            0,
            0,
            "wf_xtra_text_output_username"
        );
        (await _room.SaveAsync<UpdateAddonMessage>(BOX, intParams: [0], stringParam: "un"))
            .Should()
            .BeTrue();
        var ctx = Triggered(USER);
        ctx.Policy.TextPlaceholders.Add(placeholder);

        (await ctx.FormatTextAsync("u=$(un) x=$(nope) y=$(UN)", Ct))
            .Should()
            .Be("u=Hutch. x=$(nope) y=$(UN)");
    }

    private async Task<WiredActionMoveRotateUser> PlaceMoveUserAsync(int rotation)
    {
        var box = _room.AddBox<WiredActionMoveRotateUser>(BOX, 0, 0, "wf_act_move_rotate_user");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                intParams: [-1, rotation],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        return box;
    }

    private async Task<WiredActionFreezeUser> PlaceFreezeAsync(bool cancelOnTeleport)
    {
        var box = _room.AddBox<WiredActionFreezeUser>(BOX, 0, 0, "wf_act_freeze");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                intParams: [0, cancelOnTeleport ? 1 : 0],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        return box;
    }

    private async Task<WiredActionTeleportTo> PlaceTeleportAsync(WiredFurniSourceType furniSource)
    {
        var box = _room.AddBox<WiredActionTeleportTo>(BOX + 1, 0, 1, "wf_act_teleport_to");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX + 1,
                intParams: [0],
                stuffIds: furniSource == WiredFurniSourceType.SelectedItems ? [TARGET] : [],
                furniSources:
                [
                    [furniSource],
                ],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        return box;
    }

    private async Task<WiredActionShowMessage> PlaceShowMessageAsync(
        int id,
        int visibility,
        string text
    )
    {
        var box = _room.AddBox<WiredActionShowMessage>(id, 0, id, "wf_act_show_message");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                id,
                intParams: [visibility, 202, -1],
                stringParam: text,
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        return box;
    }

    private async Task<WiredExecutionContext> ExecuteAsync(
        WiredActionMoveRotateUser box,
        int triggerer
    )
    {
        var ctx = Triggered(triggerer);

        await box.ExecuteAsync(ctx, Ct);

        return ctx;
    }

    private WiredExecutionContext Triggered(int objectId) =>
        new(_room.Harness.Room)
        {
            Selected = new WiredSelectionSet([], [(RoomObjectId)objectId]),
            CancellationToken = Ct,
        };

    private WiredProcessingContext TriggeredBy(int objectId)
    {
        var ctx = new WiredProcessingContext(_room.Harness.Room)
        {
            Event = new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [],
            },
            Stack = _room.Harness.Fakes.Create<IWiredStack>(),
        };

        ctx.Selected.SelectedAvatarIds.Add(objectId);

        return ctx;
    }
}
