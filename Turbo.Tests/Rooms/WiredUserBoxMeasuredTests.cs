using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Avatar;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// What the user conditions and triggers were measured doing on the official hotel (wired
/// matrix W3; measured, the conditions and triggers experiments, each a pair that should and
/// should not let a move through): Triggerer Matches by the triggerer's own name and by a name
/// nobody has, User Direction with every direction ticked and with none, and Performs Action
/// on a dance of the chosen style, of another style, and on a wave.
/// </summary>
public sealed class WiredUserBoxMeasuredTests
{
    private const int BOX = 2;
    private const int PLAYER = 5;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>condition.26 named_triggerer: the triggerer's own name holds, "NoSuchProbeUser_4982" does not.</summary>
    [Theory]
    [InlineData("Alice", true)]
    [InlineData("NoSuchProbeUser_4982", false)]
    public async Task Triggerer_matches_by_name_condition_26(string name, bool holds)
    {
        _room.Enter(PLAYER, 1, 1, "Alice");

        var condition = _room.AddBox<WiredConditionExecutorMatches>(
            BOX,
            0,
            0,
            "wf_cnd_triggerer_match"
        );

        (
            await _room.SaveAsync<UpdateConditionMessage>(
                BOX,
                intParams: [1],
                stringParam: name,
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                    [WiredPlayerSourceType.UserByName],
                ]
            )
        ).Should().BeTrue();

        condition.Evaluate(TriggeredBy(PLAYER)).Should().Be(holds);
    }

    /// <summary>condition.37 user_direction_mask: 255 holds whichever way the user faces, 0 never does.</summary>
    [Theory]
    [InlineData(255, 0, true)]
    [InlineData(255, 5, true)]
    [InlineData(0, 5, false)]
    public async Task User_direction_holds_for_a_ticked_direction_condition_37(
        int mask,
        int rotation,
        bool holds
    )
    {
        _room.Enter(PLAYER, 1, 1).SetRotation((Rotation)rotation);

        var condition = _room.AddBox<WiredConditionEntityDirection>(BOX, 0, 0, "wf_cnd_actor_dir");

        (
            await _room.SaveAsync<UpdateConditionMessage>(
                BOX,
                intParams: [mask],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ]
            )
        ).Should().BeTrue();

        condition.Evaluate(TriggeredBy(PLAYER)).Should().Be(holds);
    }

    /// <summary>
    /// trigger.16 dance_trigger_matching_style / dance_trigger_wrong_style / wave_trigger: a
    /// trigger narrowed to "dance 2" fires on dance 2 only; one on wave fires on a wave.
    /// </summary>
    [Theory]
    [InlineData(WiredAvatarActionType.Dance, "dance 2", AvatarActionType.Dance, 2, true)]
    [InlineData(WiredAvatarActionType.Dance, "dance 2", AvatarActionType.Dance, 3, false)]
    [InlineData(WiredAvatarActionType.Wave, "", AvatarActionType.Expression, 1, true)]
    public async Task Performs_action_fires_on_the_chosen_dance_style_trigger_16(
        WiredAvatarActionType wanted,
        string filter,
        AvatarActionType performed,
        int value,
        bool fires
    )
    {
        _room.Enter(PLAYER, 1, 1);

        var trigger = _room.AddBox<WiredTriggerHabboPerformsAction>(
            BOX,
            0,
            0,
            "wf_trg_user_performs_action"
        );

        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                BOX,
                intParams: [(int)wanted],
                stringParam: filter
            )
        )
            .Should()
            .BeTrue();

        (
            await trigger.MatchesEventAsync(
                new AvatarPerformsActionEvent
                {
                    RoomId = 1,
                    ObjectId = PLAYER,
                    ActionType = performed,
                    Value = value,
                },
                Ct
            )
        ).Should().Be(fires);
    }

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
