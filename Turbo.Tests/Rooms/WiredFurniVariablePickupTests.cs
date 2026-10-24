using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A furni picked up to the inventory and placed again holds none of its furni variables, the
/// permanent one included: Give Variable had given a chest "fa" (available while the room is
/// active) = 3 and "fp" (permanent) = 4, and after the re-place WiredGetVariablesForObject listed
/// neither (official client capture 2026-10-24, WIN63-202610201032, explore 17-45-05 /
/// 17-45-29, wired matrix action.39.B3 / variable.0.B3, 2/2).
/// </summary>
public sealed class WiredFurniVariablePickupTests
{
    private const int CHEST = 5;
    private const int FURNI_ACTIVE = 12;
    private const int FURNI_PERMANENT = 13;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ActionContext Owner => ActionContext.CreateForSystem(1);

    private readonly WiredRoom _room = new(8, 8);

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Fact]
    public async Task A_furni_picked_up_and_placed_again_holds_none_of_its_variables()
    {
        _room.AddBox<WiredVariableFurni>(FURNI_ACTIVE, 2, 1, "wf_var_furni");
        _room.AddBox<WiredVariableFurni>(FURNI_PERMANENT, 2, 2, "wf_var_furni");
        _room.AddFloorItem(CHEST, 5, 5);

        var fa = await SaveVariableAsync(FURNI_ACTIVE, "fa", WiredAvailabilityType.RoomActive);
        var fp = await SaveVariableAsync(FURNI_PERMANENT, "fp", WiredAvailabilityType.Persistent);

        await CreateAsync(fa, 3);
        await CreateAsync(fp, 4);
        (await ValuesAsync()).Should().Contain(new[] { (fa, 3), (fp, 4) });

        (await _room.Harness.Room.RemoveItemByIdAsync(Owner, CHEST, true, Ct)).Should().BeTrue();
        await TickAsync();
        _room.AddFloorItem(CHEST, 5, 5);
        await TickAsync();

        (await ValuesAsync()).Select(x => x.Id).Should().NotContain([fa, fp]);
    }

    private async Task<WiredVariableId> SaveVariableAsync(
        int boxId,
        string name,
        WiredAvailabilityType availability
    )
    {
        (
            await _room.SaveAsync<UpdateVariableMessage>(
                boxId,
                intParams: [1, (int)availability],
                stringParam: name
            )
        )
            .Should()
            .BeTrue();
        await TickAsync();

        return ((FurnitureWiredVariableLogic)_room.FloorItem(boxId).Logic)
            .GetVarSnapshot()
            .VariableId;
    }

    private async Task CreateAsync(WiredVariableId variable, int value) =>
        (
            await _room.Harness.Room.ApplyWiredVariableMenuOperationAsync(
                Owner,
                Chest(),
                variable,
                WiredVariableMenuOperationType.Create,
                new WiredVariableValue(value),
                Ct
            )
        )
            .Should()
            .BeTrue();

    private async Task<List<(WiredVariableId Id, int Value)>> ValuesAsync() =>
        [
            .. (await _room.Harness.Room.GetAllVariablesForBindingAsync(Owner, Chest(), Ct))!.Values.Select(
                x => (x.id, x.value.ToClient())
            ),
        ];

    private static WiredVariableBinding Chest() => new(WiredVariableTargetType.Furni, CHEST);

    private async Task TickAsync()
    {
        _room.Clock.AdvanceMs(50);
        await Wired.ProcessWiredAsync(_room.Harness.NowMs(), dormant: false, Ct);
    }
}
