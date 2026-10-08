using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Wired.Variables.Furniture;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Wired names a wall item by its id made negative: the client sends a picked wall item so
/// (Flash <c>HabboUserDefinedRoomEvents.roomObjectAddedHandler</c>: <c>stuffAdded(-id)</c> for
/// category 20, and the client here does the same), reads a negative id back as a wall item, and
/// sirjonasxx's variables-info #11 says "Wall items use negative identifiers (under 0)" for
/// <c>@id</c>. The server looked the negative id up as it came and dropped every wall item a box
/// picked.
/// </summary>
public sealed class WiredWallItemIdTests
{
    private const int LAMP = 70;

    private readonly WiredRoom _room = new(8, 8);

    [Fact]
    public async Task A_picked_wall_item_is_kept_and_shown_negative()
    {
        _room.AddWallItem(LAMP);
        var box = _room.AddBox<WiredActionToggleItemState>(1, 0, 0, "wf_act_toggle_state");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                1,
                intParams: [0],
                stuffIds: [-LAMP],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        box.GetStuffIds().Should().Equal(LAMP);
        box.GetSnapshot().StuffIds.Should().Equal(-LAMP);
    }

    [Fact]
    public void A_wall_items_id_is_negative()
    {
        _room.AddWallItem(LAMP);
        var floor = _room.AddFloorItem(71, 2, 2);
        var id = new FurnitureIdVariable(_room.Harness.Room);
        var key = (int target) =>
            new WiredVariableKey(
                id.GetVarSnapshot().VariableId,
                WiredVariableTargetType.Furni,
                target
            );

        id.TryGetValue(key(LAMP), out var wall).Should().BeTrue();
        id.TryGetValue(key(71), out var floorValue).Should().BeTrue();

        wall.Value.Should().Be(-LAMP);
        floorValue.Value.Should().Be(71);
    }

    [Fact]
    public async Task The_highlighter_is_told_a_wall_holder_by_its_negative_id()
    {
        _room.AddWallItem(LAMP);
        var marked =
            _room.AddBox<Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables.WiredVariableFurni>(
                12,
                6,
                6,
                "wf_var_furni"
            );

        (
            await _room.SaveAsync<UpdateVariableMessage>(
                12,
                intParams: [(int)WiredAvailabilityType.RoomActive, 1],
                stringParam: "marked"
            )
        )
            .Should()
            .BeTrue();
        await marked.LoadWiredAsync(TestContext.Current.CancellationToken);

        var id = marked.GetVarSnapshot().VariableId;

        (
            (System.Collections.IDictionary)
                RoomHarness.GetMember(_room.Harness.Room.WiredSystem, "_variableById")!
        )[id] = marked;
        (
            await marked.GiveValueAsync(
                new WiredVariableKey(id, WiredVariableTargetType.Furni, LAMP),
                0
            )
        )
            .Should()
            .BeTrue();

        _room
            .Harness.Room.WiredSystem.GetVariableHolders(id)!
            .Holders.Select(x => x.objectId)
            .Should()
            .Equal(-LAMP);
    }
}
