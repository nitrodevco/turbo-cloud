using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The boxes that take two pick lists read each slot's own picks, and the movement physics
/// addon's "move through users" passes only the users its user source names. What their editors
/// are offered is held to Habbo's by WiredOfficialEditorContractTests.
/// </summary>
public sealed class WiredSecondaryFurniSourcesTests
{
    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PickedFurniOnASecondSlot_IsTheFirstPickList()
    {
        // The editor puts every "Use picked furnis" pick in the first list, whatever the slot;
        // only "Secondary furni picks" go to the second (dual picking mode).
        _room.AddFloorItem(10, 1, 1);
        _room.AddFloorItem(11, 2, 2);
        var box = _room.AddBox<WiredActionMoveAsGroup>(7, 0, 0, "wf_act_move_furni_as_group");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                7,
                intParams: [0, 0, 0],
                stuffIds: [10],
                stuffIds2: [11],
                furniSources:
                [
                    [WiredFurniSourceType.SecondaryItems],
                    [WiredFurniSourceType.SelectedItems],
                ],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        var ctx = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };

        WiredSlotSelection.ForSlot(box, ctx, 0).SelectedFurniIds.Should().Equal(11);
        WiredSlotSelection.ForSlot(box, ctx, 1).SelectedFurniIds.Should().Equal(10);
    }

    [Fact]
    public async Task MoveThroughUsers_PassesOnlyTheUsersOfTheAddonsUserSource()
    {
        _room.Enter(5, 1, 1);
        _room.Enter(6, 2, 1);
        _room.Enter(7, 1, 2);
        var addon = _room.AddBox<WiredAddonMovementPhysics>(8, 0, 0, "wf_xtra_mov_physics");

        (
            await _room.SaveAsync<UpdateAddonMessage>(
                8,
                intParams: [0, 0, 1, 0],
                furniSources:
                [
                    [WiredFurniSourceType.AllRoomItems],
                    [WiredFurniSourceType.AllRoomItems],
                ],
                playerSources:
                [
                    [WiredPlayerSourceType.SelectorUsers],
                ]
            )
        ).Should().BeTrue();

        var processing = new WiredProcessingContext(_room.Harness.Room)
        {
            Event = new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [],
            },
            Stack = _room.Harness.Fakes.Create<IWiredStack>(),
        };
        processing.SelectorPool.SelectedAvatarIds.Add((RoomObjectId)6);

        await addon.MutatePolicyAsync(processing, Ct);

        var execution = new WiredExecutionContext(_room.Harness.Room)
        {
            Policy = processing.Policy,
            CancellationToken = Ct,
        };
        var mover = _room.Avatars[5];

        (
            await execution.ProcessUserMovementAsync(
                mover,
                _room.Map.ToIdx(1, 2),
                SlideAvatarMoveType.Slide
            )
        )
            .Should()
            .BeFalse("user 7 is not in the add-on's user source");
        (
            await execution.ProcessUserMovementAsync(
                mover,
                _room.Map.ToIdx(2, 1),
                SlideAvatarMoveType.Slide
            )
        )
            .Should()
            .BeTrue("user 6 is");
    }

    [Fact]
    public async Task MoveThroughUsers_StillStopsAtAFurniNobodyCanStandOn()
    {
        _room.Enter(5, 1, 1);
        _room.AddFloorItem(20, 2, 1, canWalk: false);
        var execution = new WiredExecutionContext(_room.Harness.Room)
        {
            Policy = new WiredPolicy { MovePhysics = WiredMovePhysicsFlags.MoveThroughUsers },
            CancellationToken = Ct,
        };

        (
            await execution.ProcessUserMovementAsync(
                _room.Avatars[5],
                _room.Map.ToIdx(2, 1),
                SlideAvatarMoveType.Slide
            )
        )
            .Should()
            .BeFalse("the flag lets a user past users, not onto a table");
        _room.Positions().Should().Equal("5@1,1");
    }

    [Fact]
    public async Task MoveThroughUsers_PassesEveryUser_WithTheDefaultSource()
    {
        _room.Enter(5, 1, 1);
        _room.Enter(7, 1, 2);
        var addon = _room.AddBox<WiredAddonMovementPhysics>(8, 0, 0, "wf_xtra_mov_physics");

        (await _room.SaveAsync<UpdateAddonMessage>(8, intParams: [0, 0, 1, 0])).Should().BeTrue();

        var processing = new WiredProcessingContext(_room.Harness.Room)
        {
            Event = new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [],
            },
            Stack = _room.Harness.Fakes.Create<IWiredStack>(),
        };

        await addon.MutatePolicyAsync(processing, Ct);

        var execution = new WiredExecutionContext(_room.Harness.Room)
        {
            Policy = processing.Policy,
            CancellationToken = Ct,
        };

        (
            await execution.ProcessUserMovementAsync(
                _room.Avatars[5],
                _room.Map.ToIdx(1, 2),
                SlideAvatarMoveType.Slide
            )
        )
            .Should()
            .BeTrue();
    }
}
