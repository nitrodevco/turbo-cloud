using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A furni picked up from the room leaves the picks of every box that held it at once: Habbo
/// re-opened a Move and Rotate that picked a chair and a block, the chair picked up, with the
/// block alone, and its next firing moved the block (official client capture 2026-10-24T07-44-58,
/// WIN63-202610201032, 1/1).
/// </summary>
public sealed class WiredPickedUpPicksTests
{
    private const int CHAIR = 20;
    private const int BLOCK = 21;

    private readonly WiredRoom _room = new(8, 8);

    // action.4.R4
    [Fact]
    public async Task Action4R4_APickedUpFurniLeavesTheBoxsPicksAtOnce()
    {
        _room.AddFloorItem(CHAIR, 3, 3);
        _room.AddFloorItem(BLOCK, 4, 4);
        var box = _room.AddBox<WiredActionMoveRotateFurni>(1, 0, 0, "wf_act_move_rotate");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                1,
                intParams: [1, 0],
                stuffIds: [CHAIR, BLOCK],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();
        box.GetSnapshot().StuffIds.Should().Equal(CHAIR, BLOCK);

        (
            await _room
                .Harness.Module<RoomObjectModule>()
                .RemoveObjectAsync(
                    ActionContext.CreateForSystem(1),
                    _room.FloorItem(CHAIR),
                    TestContext.Current.CancellationToken
                )
        )
            .Should()
            .BeTrue();

        box.GetSnapshot().StuffIds.Should().Equal(BLOCK);
        box.GetStuffIds().Should().Equal(BLOCK);
    }
}
