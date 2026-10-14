using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The sources the editor is offered for the boxes that take two pick lists, as the official
/// client capture shows them: every slot offers both pick lists ("Use picked furnis" 100 and
/// "Secondary furni picks" 101) with the pool sources, and each slot has its own default. The
/// movement physics addon also gets its three source sections, defaulting to the whole room,
/// and "move through users" passes only the users its user source names.
/// </summary>
public sealed class WiredSecondaryFurniSourcesTests
{
    private static readonly int[] Both = [0, 100, 101, 200, 201];
    private static readonly int[] Users = [0, 200, 201];

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string, int[][], int[], int[][], int[]> OfficialContracts =>
        new()
        {
            // name, allowed furni, default furni, allowed users, default users
            { "wf_act_send_signal", [Both, Both], [100, 200], [Users], [200] },
            {
                "wf_act_init_transaction",
                [Both, Both, Both],
                [100, 101, 200],
                [Users, Users],
                [0, 200]
            },
            { "wf_cnd_chest_has_item_type", [Both, Both, Both], [100, 101, 0], [Users], [0] },
            { "wf_xtra_scan_chest_furni_by_type", [Both, Both], [0, 100], [], [] },
            { "wf_act_give_furni", [Both, Both], [100, 200], [Users, Users], [0, 200] },
            { "wf_act_give_currency", [Both, Both], [100, 200], [Users, Users], [0, 200] },
            {
                "wf_xtra_custom_contract",
                [Both, Both, Both, Both],
                [0, 0, 200, 200],
                [Users, Users],
                [200, 200]
            },
            { "wf_act_move_furni_as_group", [Both, Both], [100, 0], [Users], [0] },
            { "wf_act_furni_to_furni", [Both, Both], [0, 100], [], [] },
            {
                "wf_cnd_chest_has_items",
                [
                    [0, 100, 200, 201],
                    [0, 100, 200, 201],
                ],
                [100, 200],
                [Users],
                [200]
            },
            {
                "wf_xtra_mov_physics",
                [
                    [.. Both, 900],
                    [.. Both, 900],
                ],
                [900, 900],
                [
                    [.. Users, 900],
                ],
                [900]
            },
        };

    [Theory]
    [MemberData(nameof(OfficialContracts))]
    public void TheEditorIsOfferedTheOfficialSourcesAndDefaults(
        string logic,
        int[][] furni,
        int[] defaultFurni,
        int[][] users,
        int[] defaultUsers
    )
    {
        var box = AddBox(logic);

        box.GetAllowedFurniSources()
            .Select(Sorted<WiredFurniSourceType>(WiredFurniSourceTypeExtensions.GetProtocolId))
            .Should()
            .BeEquivalentTo(furni, o => o.WithStrictOrdering());
        box.GetDefaultFurniSources()
            .Select(slot => (int)WiredFurniSourceTypeExtensions.GetProtocolId(slot.Single()))
            .Should()
            .Equal(defaultFurni);
        box.GetAllowedPlayerSources()
            .Select(Sorted<WiredPlayerSourceType>(WiredPlayerSourceTypeExtensions.GetProtocolId))
            .Should()
            .BeEquivalentTo(users, o => o.WithStrictOrdering());
        box.GetDefaultPlayerSources()
            .Select(slot => (int)WiredPlayerSourceTypeExtensions.GetProtocolId(slot.Single()))
            .Should()
            .Equal(defaultUsers);
    }

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

    private static Func<T[], int[]> Sorted<T>(Func<T, WiredSourceType> toWire) =>
        slot => [.. slot.Select(x => (int)toWire(x)).Order()];

    private FurnitureWiredLogic AddBox(string logic) =>
        logic switch
        {
            "wf_act_send_signal" => _room.AddBox<WiredActionSendSignal>(1, 0, 0, logic),
            "wf_act_init_transaction" => _room.AddBox<WiredActionInitiateTransaction>(
                1,
                0,
                0,
                logic
            ),
            "wf_cnd_chest_has_item_type" => _room.AddBox<WiredConditionChestHasItemTypes>(
                1,
                0,
                0,
                logic
            ),
            "wf_cnd_chest_has_items" => _room.AddBox<WiredConditionChestHasItems>(1, 0, 0, logic),
            "wf_xtra_scan_chest_furni_by_type" => _room.AddBox<WiredAddonChestItemTypeScanner>(
                1,
                0,
                0,
                logic
            ),
            "wf_act_give_furni" => _room.AddBox<WiredActionGiveFurniFromChest>(1, 0, 0, logic),
            "wf_act_give_currency" => _room.AddBox<WiredActionGiveCurrencyFromChest>(
                1,
                0,
                0,
                logic
            ),
            "wf_xtra_custom_contract" => _room.AddBox<WiredAddonCustomContract>(1, 0, 0, logic),
            "wf_act_move_furni_as_group" => _room.AddBox<WiredActionMoveAsGroup>(1, 0, 0, logic),
            "wf_act_furni_to_furni" => _room.AddBox<WiredActionFurniToFurni>(1, 0, 0, logic),
            "wf_xtra_mov_physics" => _room.AddBox<WiredAddonMovementPhysics>(1, 0, 0, logic),
            _ => throw new ArgumentOutOfRangeException(nameof(logic), logic, null),
        };
}
