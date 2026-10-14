using System.Collections;
using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Variables.User;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Habbo sends <c>@handitem</c> (-240) with canCreateAndDelete on (hhus, WIN63-202610121709), so
/// Give Variable and Remove Variable offer it. Ours advertised the flag but neither box did
/// anything with it (tester report 2026-10-14: "giving yourself a handitem variable does
/// nothing; the same stack works on official").
/// </summary>
public sealed class WiredGiveHandItemVariableTests
{
    private const int PLAYER_INDEX = 5;
    private const int GIVE = 2;
    private const int REMOVE = 3;
    private const string HAND_ITEM = "-240";

    private readonly WiredRoom _room = new(8, 8);

    public WiredGiveHandItemVariableTests()
    {
        var handItem = new UserHandItemVariable(_room.Harness.Room);

        ((IDictionary)RoomHarness.GetMember(_room.Harness.Room.WiredSystem, "_variableById")!)[
            handItem.GetVarSnapshot().VariableId
        ] = handItem;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Giving_handitem_puts_the_item_in_an_empty_hand()
    {
        var avatar = _room.Enter(PLAYER_INDEX, 1, 1);
        var give = await GiveBoxAsync(GIVE, initial: 7, replace: false);

        (await give.ExecuteAsync(Selecting(), Ct)).Should().BeTrue();
        avatar.HandItemId.Should().Be(7);
    }

    [Fact]
    public async Task A_held_item_is_kept_unless_the_box_overrides()
    {
        var avatar = _room.Enter(PLAYER_INDEX, 1, 1);
        avatar.SetHandItem(3);

        (await (await GiveBoxAsync(GIVE, initial: 7, replace: false)).ExecuteAsync(Selecting(), Ct))
            .Should()
            .BeFalse();
        avatar.HandItemId.Should().Be(3);

        (
            await (await GiveBoxAsync(GIVE + 10, initial: 7, replace: true)).ExecuteAsync(
                Selecting(),
                Ct
            )
        )
            .Should()
            .BeTrue();
        avatar.HandItemId.Should().Be(7);
    }

    [Fact]
    public async Task Removing_handitem_empties_the_hand()
    {
        var avatar = _room.Enter(PLAYER_INDEX, 1, 1);
        avatar.SetHandItem(3);
        var remove = _room.AddBox<WiredActionRemoveVariable>(REMOVE, 0, 1, "wf_act_remove_var");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                REMOVE,
                intParams: [(int)WiredVariableTargetType.User],
                definitionSpecifics: [0],
                variableIds: [HAND_ITEM]
            )
        )
            .Should()
            .BeTrue();

        (await remove.ExecuteAsync(Selecting(), Ct)).Should().BeTrue();
        avatar.HandItemId.Should().Be(0);
    }

    private async Task<WiredActionGiveVariable> GiveBoxAsync(int id, int initial, bool replace)
    {
        var give = _room.AddBox<WiredActionGiveVariable>(id, 0, id % 8, "wf_act_give_var");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                id,
                intParams: [(int)WiredVariableTargetType.User, 0, initial, replace ? 1 : 0],
                definitionSpecifics: [0],
                variableIds: [HAND_ITEM]
            )
        )
            .Should()
            .BeTrue();

        return give;
    }

    private WiredExecutionContext Selecting()
    {
        var ctx = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };

        ctx.Selected.SelectedAvatarIds.Add(PLAYER_INDEX);

        return ctx;
    }
}
