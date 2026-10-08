using System.Collections;
using FluentAssertions;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Counters;
using Turbo.Rooms.Wired.Variables;
using Turbo.Rooms.Wired.Variables.Furniture.Smart;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Smart variables (<c>~name</c>, Wired Faculty: the list released on 12/12/2024): a kind of furni
/// brings them, and the variable list has them only while one is in the room. A room linker (a
/// teleport) leads to item 77 through <c>~teleport.target_id</c>, which relinks it when written; a
/// counter clock's <c>~clock.pulse_count</c> is its time in half seconds.
/// </summary>
public sealed class WiredSmartVariableTests
{
    private const int LINKER = 30;
    private const int CLOCK = 31;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_smart_variable_is_listed_only_while_its_furni_is_in_the_room()
    {
        var variable = new FurnitureTeleportTargetVariable(_room.Harness.Room);
        var id = variable.GetVarSnapshot().VariableId;
        var byId = (IDictionary)
            RoomHarness.GetMember(_room.Harness.Room.WiredSystem, "_variableById")!;

        byId[id] = variable;

        (await Listed()).Should().NotContain(id);

        AddLinker();

        (await Listed()).Should().Contain(id);
        variable.GetVarSnapshot().VariableType.Should().Be(WiredVariableType.Smart);
        variable.GetVarSnapshot().VariableName.Should().Be("~teleport.target_id");
    }

    [Fact]
    public async Task Teleport_target_reads_the_pair_and_relinks_when_written()
    {
        var logic = AddLinker();
        var variable = new FurnitureTeleportTargetVariable(_room.Harness.Room);
        var key = Key(variable, LINKER);

        variable.TryGetValue(key, out var value).Should().BeTrue();
        ((int)value).Should().Be(77);

        (
            await variable.SetValueAsync(
                _room.Harness.Fakes.Create<IWiredExecutionContext>(),
                key,
                88
            )
        )
            .Should()
            .BeTrue();
        logic.PartnerItemId.Should().Be(88);
    }

    [Fact]
    public async Task Clock_pulse_count_is_its_half_seconds_and_sets_it()
    {
        var clock = (FurnitureCounterClockLogic)
            _room
                .AddFloorItem(
                    CLOCK,
                    2,
                    2,
                    "wf_upcounter",
                    createLogic: (stuffData, ctx) => new FurnitureCounterClockLogic(stuffData, ctx)
                )
                .Logic;
        var variable = new FurnitureClockPulseCountVariable(_room.Harness.Room);
        var key = Key(variable, CLOCK);

        (
            await variable.SetValueAsync(
                _room.Harness.Fakes.Create<IWiredExecutionContext>(),
                key,
                120
            )
        )
            .Should()
            .BeTrue();
        clock.HalfSeconds.Should().Be(120);

        variable.TryGetValue(key, out var value).Should().BeTrue();
        ((int)value).Should().Be(120);

        // Not a clock: no value.
        variable.TryGetValue(Key(variable, LINKER), out _).Should().BeFalse();
    }

    private FurnitureTeleportLogic AddLinker()
    {
        var item = _room.AddFloorItem(
            LINKER,
            1,
            1,
            TeleportFurniture.LOGIC_NAME,
            createLogic: (stuffData, ctx) => new FurnitureTeleportLogic(stuffData, ctx)
        );

        item.SetExtraData(TeleportFurniture.PairExtraData(77));

        return (FurnitureTeleportLogic)item.Logic;
    }

    private async Task<List<WiredVariableId>> Listed() =>
        [
            .. (
                await _room.Harness.Room.WiredSystem.GetWiredVariablesSnapshotAsync(Ct)
            ).Variables.Select(x => x.VariableId),
        ];

    private static WiredVariableKey Key(WiredInternalVariable variable, int itemId) =>
        new(variable.GetVarSnapshot().VariableId, WiredVariableTargetType.Furni, itemId);
}
