using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Give Variable's initial value is a long in two ints (Flash <c>Util.pushIntAsLong</c>: -1 then
/// the value for a negative one). The high word was held to 0, so a box with a negative initial
/// value could not be saved at all (tester report 2026-10-09: "give variable doesn't work").
/// </summary>
public sealed class WiredGiveVariableTests
{
    private const int PLAYER_INDEX = 5;
    private const int MARK = 20;
    private const int BOX = 2;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(7)]
    [InlineData(-5)]
    public async Task The_initial_value_is_given_whatever_its_sign(int initial)
    {
        var mark = await PlaceMarkAsync();
        var give = _room.AddBox<WiredActionGiveVariable>(BOX, 0, 0, "wf_act_give_var");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                intParams: [(int)WiredVariableTargetType.User, initial < 0 ? -1 : 0, initial, 0],
                definitionSpecifics: [0],
                variableIds: [mark.GetVarSnapshot().VariableId.ToString()]
            )
        )
            .Should()
            .BeTrue();

        var ctx = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };

        ctx.Selected.SelectedAvatarIds.Add(PLAYER_INDEX);

        (await give.ExecuteAsync(ctx, Ct)).Should().BeTrue();
        mark.TryGetValue(
                new WiredVariableKey(
                    mark.GetVarSnapshot().VariableId,
                    WiredVariableTargetType.User,
                    PLAYER_INDEX
                ),
                out var value
            )
            .Should()
            .BeTrue();
        value.Should().Be(new WiredVariableValue(initial));
    }

    /// <summary>
    /// action.39.R1: given to the selector's users and the selector found nobody (Users by Name
    /// "nobody"), nobody gets the variable, the triggering user neither (official client capture
    /// 2026-10-24T18-34-22, W5 lab, 2/2).
    /// </summary>
    [Fact]
    public async Task An_empty_user_selection_gives_nobody_not_the_triggerer_action_39_R1()
    {
        var mark = await PlaceMarkAsync();
        var give = _room.AddBox<WiredActionGiveVariable>(BOX, 0, 0, "wf_act_give_var");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                intParams: [(int)WiredVariableTargetType.User, 0, 5, 0],
                definitionSpecifics: [0],
                playerSources:
                [
                    [WiredPlayerSourceType.SelectorUsers],
                ],
                variableIds: [mark.GetVarSnapshot().VariableId.ToString()]
            )
        ).Should().BeTrue();

        var ctx = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };

        ctx.Selected.SelectedAvatarIds.Add(PLAYER_INDEX);

        (await give.ExecuteAsync(ctx, Ct)).Should().BeFalse();
        mark.TryGetValue(
                new WiredVariableKey(
                    mark.GetVarSnapshot().VariableId,
                    WiredVariableTargetType.User,
                    PLAYER_INDEX
                ),
                out _
            )
            .Should()
            .BeFalse();
    }

    /// <summary>
    /// Give Variable with "override" fires Variable Changed as a creation both when the user had
    /// no value and when it replaces one; only a change of an existing value is an update
    /// (official client capture: the created-only trigger fired on both gives, not on Change
    /// Variable).
    /// </summary>
    [Fact]
    public async Task A_give_that_overrides_a_value_is_announced_as_a_creation()
    {
        var mark = await PlaceMarkAsync();
        var heard = new List<RoomEvent>();
        _room.Harness.Module<RoomEventModule>().Register(new Recorder(heard));
        var key = new WiredVariableKey(
            mark.GetVarSnapshot().VariableId,
            WiredVariableTargetType.User,
            PLAYER_INDEX
        );

        (await mark.GiveValueAsync(key, new WiredVariableValue(1), replace: true))
            .Should()
            .BeTrue();
        (await mark.GiveValueAsync(key, new WiredVariableValue(2), replace: true))
            .Should()
            .BeTrue();
        (
            await mark.SetValueAsync(
                new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct },
                key,
                new WiredVariableValue(3)
            )
        )
            .Should()
            .BeTrue();

        heard
            .OfType<WiredVariableChangedEvent>()
            .Select(changed => changed.ChangeType)
            .Should()
            .Equal(
                WiredVariableChangeType.Created,
                WiredVariableChangeType.Created,
                WiredVariableChangeType.Updated
            );
    }

    // variable.0.R3 / variable.1.R3: a give flashes the variable box to 6, the +1 changes after
    // it 2 then 3 (official client capture 2026-10-24T15-54-49 user variable, 22-59-11 furni
    // variable: ObjectsDataUpdate 6, 2, 3, 2).
    [Fact]
    public async Task A_give_flashes_the_variable_box_to_6()
    {
        var mark = await PlaceMarkAsync();
        var key = new WiredVariableKey(
            mark.GetVarSnapshot().VariableId,
            WiredVariableTargetType.User,
            PLAYER_INDEX
        );
        var ctx = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };

        (await mark.GiveValueAsync(key, new WiredVariableValue(5), replace: true))
            .Should()
            .BeTrue();
        mark.GetState().Should().Be(6);

        (await mark.SetValueAsync(ctx, key, new WiredVariableValue(6))).Should().BeTrue();
        mark.GetState().Should().Be(2);

        (await mark.SetValueAsync(ctx, key, new WiredVariableValue(7))).Should().BeTrue();
        mark.GetState().Should().Be(3);
    }

    // action.39.B2 / variable.1.B2: a delayed Give Variable (override on) whose user left the
    // room before the delay ended gives nothing: after re-entering, the permanent variable still
    // holds its old value and the while-in-room one is gone (official client capture 2026-10-24,
    // WIN63-202610201032, 2/2). Ours made the while-in-room value a store of its own again under
    // the room index the user had left, which the next avatar given that index read.
    [Theory]
    [InlineData(WiredAvailabilityType.UserActive, null)]
    [InlineData(WiredAvailabilityType.Persistent, 7L)]
    public async Task Action39B2_AGiveReachingAUserWhoLeftIsLost(
        WiredAvailabilityType availability,
        long? afterReentry
    )
    {
        var mark = await PlaceMarkAsync(availability);
        var give = _room.AddBox<WiredActionGiveVariable>(BOX, 0, 0, "wf_act_give_var");
        var key = new WiredVariableKey(
            mark.GetVarSnapshot().VariableId,
            WiredVariableTargetType.User,
            PLAYER_INDEX
        );

        async Task GiveAsync(int value)
        {
            (
                await _room.SaveAsync<UpdateActionMessage>(
                    BOX,
                    intParams: [(int)WiredVariableTargetType.User, 0, value, 1],
                    definitionSpecifics: [0],
                    variableIds: [mark.GetVarSnapshot().VariableId.ToString()]
                )
            )
                .Should()
                .BeTrue();

            // The user the box picked when it fired, still in the selection after the delay.
            var ctx = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };

            ctx.Selected.SelectedAvatarIds.Add(PLAYER_INDEX);
            await give.ExecuteAsync(ctx, Ct);
        }

        await GiveAsync(7);

        var avatar = _room.Avatars[PLAYER_INDEX];

        _room.Map.RemoveAvatar(avatar, false);
        _room.Harness.State.AvatarsByObjectId.Remove(PLAYER_INDEX);
        _room.Harness.State.AvatarsByPlayerId.Remove(avatar.PlayerId);
        await _room
            .Harness.Module<RoomWiredSystem>()
            .OnRoomEventAsync(
                new PlayerLeftEvent
                {
                    RoomId = 1,
                    CausedBy = ActionContext.CreateForSystem(1),
                    PlayerId = avatar.PlayerId,
                    ObjectId = PLAYER_INDEX,
                },
                Ct
            );

        mark.GetState().Should().Be(6);
        await GiveAsync(17);

        // Yet the variable box flashes as for a give (ObjectsDataUpdate 7 at the delay end,
        // 2026-10-24T18-17-45, 2/2); a give over an empty selection does not (wlab11 3/3).
        mark.GetState().Should().Be(7);
        _room.Enter(PLAYER_INDEX, 1, 1);

        if (afterReentry is { } kept)
        {
            mark.TryGetValue(key, out var value).Should().BeTrue();
            value.Should().Be(new WiredVariableValue(kept));
        }
        else
            mark.TryGetValue(key, out _).Should().BeFalse();
    }

    private async Task<WiredVariableUser> PlaceMarkAsync(
        WiredAvailabilityType availability = WiredAvailabilityType.UserActive
    )
    {
        _room.Enter(PLAYER_INDEX, 1, 1);
        var mark = _room.AddBox<WiredVariableUser>(MARK, 6, 6, "wf_var_user");
        (
            await _room.SaveAsync<UpdateVariableMessage>(
                MARK,
                intParams: [(int)availability, 1],
                stringParam: "mark"
            )
        )
            .Should()
            .BeTrue();
        await mark.LoadWiredAsync(Ct);
        await _room
            .Harness.Module<RoomWiredSystem>()
            .OnRoomEventAsync(
                new RoomWiredStackChangedEvent
                {
                    RoomId = 1,
                    CausedBy = ActionContext.CreateForSystem(1),
                    StackIds = [_room.Map.ToIdx(6, 6)],
                },
                Ct
            );
        await _room.Harness.Module<RoomWiredSystem>().ProcessWiredAsync(10_000, dormant: false, Ct);

        return mark;
    }

    private sealed class Recorder(List<RoomEvent> heard) : IRoomEventListener
    {
        public Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct)
        {
            heard.Add(evt);

            return Task.CompletedTask;
        }
    }
}
