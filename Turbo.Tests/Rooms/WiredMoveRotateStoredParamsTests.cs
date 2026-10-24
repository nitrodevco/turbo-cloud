using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// W1 action.4.E1: Move and Rotate Furni opens fresh with three ints, [0,0,1], of which its
/// editor sends two; the official server stores a two-int save as [move, rotate, 0] (official
/// client capture 2026-10-24, W1 lab, 2/2). Ours kept the third at 1. The fresh defaults are
/// WiredOfficialFreshDefaultsTests'.
/// </summary>
public sealed class WiredMoveRotateStoredParamsTests
{
    private readonly WiredRoom _room = new(8, 8);

    [Fact]
    public async Task A_save_of_the_editors_two_ints_stores_the_third_as_0()
    {
        _room.AddFloorItem(20, 3, 3);
        var box = _room.AddBox<WiredActionMoveRotateFurni>(1, 0, 0, "wf_act_move_rotate");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                1,
                intParams: [5, 1],
                stuffIds: [20],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        box.GetSnapshot().IntParams.Should().Equal(5, 1, 0);
    }
}
