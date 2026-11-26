using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Pet;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired.Variables.User;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Who holds a variable, and for how long. <c>@handitem</c> is held only by a full hand (the
/// Flash client lists it with "can create/delete" and without "is always available"). What a
/// variable box created goes when the box is picked up, and what an avatar held goes when the
/// avatar does, a pet as well: both used to stay in the room's stores for as long as it was
/// loaded, so the same box put down again found its old values.
/// </summary>
public sealed class WiredVariableHoldersTests
{
    private const int USER = 5;
    private const int MARK = 20;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredVariableHoldersTests() => _room.Enter(USER, 1, 1);

    [Fact]
    public async Task Handitem_is_held_only_by_a_full_hand()
    {
        var handItem = new UserHandItemVariable(_room.Harness.Room);
        var key = new WiredVariableKey(
            handItem.GetVarSnapshot().VariableId,
            WiredVariableTargetType.User,
            USER
        );

        handItem.TryGetValue(key, out _).Should().BeFalse("the hand is empty");

        await _room.Harness.Module<RoomAvatarModule>().SetHandItemAsync(_room.Avatars[USER], 3, Ct);

        handItem.TryGetValue(key, out var held).Should().BeTrue();
        held.Should().Be(new WiredVariableValue(3));
    }

    [Fact]
    public async Task A_picked_up_variable_box_takes_its_values_with_it()
    {
        var mark = await PlaceMarkAsync();
        var key = Key(mark);

        (await mark.GiveValueAsync(key, new WiredVariableValue(7), replace: true))
            .Should()
            .BeTrue();

        await mark.OnPickupAsync(_room.ContextFor(USER), Ct);

        mark.TryGetValue(key, out _).Should().BeFalse();
    }

    [Fact]
    public async Task A_pet_taken_out_of_the_room_lets_go_of_what_it_held()
    {
        var mark = await PlaceMarkAsync();
        var key = Key(mark);

        (await mark.GiveValueAsync(key, new WiredVariableValue(7), replace: true))
            .Should()
            .BeTrue();

        // The holder leaves as a pet does: no trigger hears it, the values go at once.
        await Wired.OnRoomEventAsync(
            new PetRemovedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                ObjectId = USER,
            },
            Ct
        );

        mark.TryGetValue(key, out _).Should().BeFalse();
    }

    // A permanent variable's store is written into its box once per execution, not per write;
    // what a stack gave must still be there when the execution is over.
    [Fact]
    public async Task What_a_stack_gives_a_permanent_variable_is_written_to_its_box()
    {
        var mark = await PlaceMarkAsync(WiredAvailabilityType.Persistent);

        _room.AddBox<WiredTriggerHabboJoinRoom>(1, 0, 0, "wf_trg_enter_room");
        _room.AddBox<WiredActionGiveVariable>(2, 0, 0, "wf_act_give_var");
        (await _room.SaveAsync<UpdateTriggerMessage>(1)).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                2,
                intParams: [(int)WiredVariableTargetType.User, 0, 7, 1],
                definitionSpecifics: [0],
                variableIds: [mark.GetVarSnapshot().VariableId.ToString()]
            )
        )
            .Should()
            .BeTrue();
        await StackChangedAsync(0, 0);

        await Wired.OnRoomEventAsync(
            new PlayerEnterEvent
            {
                RoomId = 1,
                CausedBy = _room.ContextFor(USER),
                PlayerId = 100 + USER,
            },
            Ct
        );
        await TickAsync();

        mark.TryGetValue(Key(mark), out var given).Should().BeTrue();
        given.Should().Be(new WiredVariableValue(7));
        _room
            .FloorItem(MARK)
            .ExtraData.TryGetSection(ExtraDataSectionType.STORAGE, out var stored)
            .Should()
            .BeTrue();
        stored.GetRawText().Should().Contain(":7");
    }

    private static WiredVariableKey Key(WiredVariableUser mark) =>
        new(mark.GetVarSnapshot().VariableId, WiredVariableTargetType.User, USER);

    private async Task<WiredVariableUser> PlaceMarkAsync(
        WiredAvailabilityType availability = WiredAvailabilityType.UserActive
    )
    {
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
        await StackChangedAsync(6, 6);

        return mark;
    }

    private async Task StackChangedAsync(int x, int y)
    {
        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(x, y)],
            },
            Ct
        );
        await TickAsync();
    }

    private Task TickAsync()
    {
        _now += 1_000;

        return Wired.ProcessWiredAsync(_now, dormant: false, Ct);
    }
}
