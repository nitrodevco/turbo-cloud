using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Picking up a variable box (official client capture 2026-10-24, WIN63-202610201032, wired
/// matrix variable.0.R2 / variable.2.R2): the values it held are gone (WiredGetVariablesForObject
/// lists the variable on no furni, nor on the room), and the boxes that named it keep its id, so
/// they still open with the dangling id (Give Variable ["315209", ...], Change Variable Value
/// ["315210","0"]).
/// </summary>
public sealed class WiredVariableBoxPickupTests
{
    private const int OWNER = 1;
    private const int BLOCK = 20;
    private const int VARIABLE = 30;
    private const int REFERENCE = 31;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Fact]
    public async Task Variable0R2_APickedUpFurniVariableLeavesNoValueAndADanglingReference()
    {
        _room.AddFloorItem(BLOCK, 2, 2);
        var variable = _room.AddBox<WiredVariableFurni>(VARIABLE, 4, 4, "wf_var_furni");
        var id = await SaveVariableAsync(
            variable,
            [(int)WiredAvailabilityType.RoomActive, 1],
            "wv"
        );
        var give = _room.AddBox<WiredActionGiveVariable>(REFERENCE, 0, 0, "wf_act_give_var");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                REFERENCE,
                intParams: [(int)WiredVariableTargetType.Furni, 0, 3, 0, 0, -10, 0],
                definitionSpecifics: [0],
                variableIds: [id, "n"]
            )
        )
            .Should()
            .BeTrue();
        var key = new WiredVariableKey(
            variable.GetVarSnapshot().VariableId,
            WiredVariableTargetType.Furni,
            BLOCK
        );
        (await variable.GiveValueAsync(key, 3)).Should().BeTrue();
        (await ListedOn(WiredVariableTargetType.Furni, BLOCK)).Should().Contain(id);

        await PickUpAsync(VARIABLE);

        (await ListedOn(WiredVariableTargetType.Furni, BLOCK)).Should().NotContain(id);
        Opens(give).Should().Equal(id, "0");
    }

    [Fact]
    public async Task Variable2R2_APickedUpRoomVariableIsGoneAndItsReferenceDangles()
    {
        var variable = _room.AddBox<WiredVariableRoom>(VARIABLE, 4, 4, "wf_var_room");
        var id = await SaveVariableAsync(variable, [(int)WiredAvailabilityType.RoomActive], "wr");
        var change = _room.AddBox<WiredActionChangeVariable>(
            REFERENCE,
            0,
            0,
            "wf_act_change_var_val"
        );
        (
            await _room.SaveAsync<UpdateActionMessage>(
                REFERENCE,
                intParams:
                [
                    (int)WiredVariableTargetType.Global,
                    (int)WiredVariableOperationType.Set,
                    0,
                    0,
                    1,
                    (int)WiredVariableTargetType.Global,
                    0,
                ],
                definitionSpecifics: [0],
                variableIds: [id, "0"]
            )
        )
            .Should()
            .BeTrue();
        (await ListedOn(WiredVariableTargetType.Global, 0)).Should().Contain(id);

        await PickUpAsync(VARIABLE);

        (await ListedOn(WiredVariableTargetType.Global, 0)).Should().NotContain(id);
        Opens(change).Should().Equal(id, "0");
    }

    private async Task<string> SaveVariableAsync(
        FurnitureWiredVariableLogic variable,
        int[] intParams,
        string name
    )
    {
        (
            await _room.SaveAsync<UpdateVariableMessage>(
                VARIABLE,
                intParams: intParams,
                stringParam: name
            )
        )
            .Should()
            .BeTrue();
        await variable.LoadWiredAsync(Ct);
        await RebuildAsync();

        return variable.GetVarSnapshot().VariableId.ToString();
    }

    /// <summary>The owner picks the box up (confirming the variable warning) and the room rebuilds.</summary>
    private async Task PickUpAsync(int boxId)
    {
        (
            await _room.Harness.Room.RemoveItemByIdAsync(
                ActionContext.CreateForPlayer(OWNER, 1),
                boxId,
                true,
                Ct
            )
        )
            .Should()
            .BeTrue();
        await RebuildAsync();
    }

    private async Task RebuildAsync()
    {
        await Wired.OnRoomEventAsync(
            new WiredVariableBoxChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                BoxIds = [VARIABLE],
            },
            Ct
        );
        _room.Harness.Clock.AdvanceMs(50);
        await Wired.ProcessWiredAsync(_room.Harness.NowMs(), dormant: false, Ct);
    }

    /// <summary>The variable ids the wired menu lists on a holder (WiredGetVariablesForObject).</summary>
    private async Task<List<string>> ListedOn(WiredVariableTargetType type, int targetId) =>
        [
            .. (
                await Wired.GetAllVariablesForBindingAsync(
                    new WiredVariableBinding(type, targetId),
                    Ct
                )
            ).Select(entry => entry.id.ToString()),
        ];

    /// <summary>The variable ids the box's editor opens with.</summary>
    private static List<string> Opens(FurnitureWiredLogic box) =>
        [.. box.GetSnapshot().VariableIds.Select(id => id.ToString())];
}
