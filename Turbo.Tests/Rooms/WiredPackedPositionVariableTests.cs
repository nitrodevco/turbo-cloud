using System.Collections;
using FluentAssertions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Wired.Variables;
using Turbo.Rooms.Wired.Variables.Furniture;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// <c>@position</c> and <c>@occupation</c> (official, October 2026), with the numbers of the Wired
/// Faculty tutorial "Info on @position and @occupation": x 13, y 16 is 3344; x 1, y 5, rotation 3
/// is 66819; writing 1285 moves a chair from 1,1 to 5,5 in one move.
/// </summary>
public sealed class WiredPackedPositionVariableTests
{
    private const int CHAIR = 20;

    private readonly WiredRoom _room = new(20, 20);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Position_packs_x_and_y_into_one_value()
    {
        _room.AddFloorItem(CHAIR, 13, 16);
        var variable = Register(new FurniturePositionVariable(_room.Harness.Room));

        Read(variable).Should().Be(3344);
    }

    [Fact]
    public void Occupation_packs_x_y_and_rotation_into_one_value()
    {
        var chair = _room.AddFloorItem(CHAIR, 1, 5);
        chair.SetRotation((Rotation)3);
        var variable = Register(new FurnitureOccupationVariable(_room.Harness.Room));

        Read(variable).Should().Be(66819);
    }

    [Fact]
    public async Task Writing_position_moves_the_furni_to_both_coordinates_at_once()
    {
        var chair = _room.AddFloorItem(CHAIR, 1, 1);
        var variable = Register(new FurniturePositionVariable(_room.Harness.Room));

        (await Write(variable, 1285)).Should().BeTrue();

        (chair.X, chair.Y).Should().Be((5, 5));
    }

    [Fact]
    public async Task Writing_occupation_moves_and_turns_the_furni()
    {
        var chair = _room.AddFloorItem(CHAIR, 1, 1);
        var variable = Register(new FurnitureOccupationVariable(_room.Harness.Room));

        (await Write(variable, (4 << 16) | (6 << 8) | 2)).Should().BeTrue();

        (chair.X, chair.Y, chair.Rotation).Should().Be((4, 6, Rotation.East));
    }

    private WiredInternalVariable Register(WiredInternalVariable variable)
    {
        ((IDictionary)RoomHarness.GetMember(_room.Harness.Room.WiredSystem, "_variableById")!)[
            variable.GetVarSnapshot().VariableId
        ] = variable;

        return variable;
    }

    private static WiredVariableKey Key(WiredInternalVariable variable) =>
        new(variable.GetVarSnapshot().VariableId, WiredVariableTargetType.Furni, CHAIR);

    private static long Read(WiredInternalVariable variable)
    {
        variable.TryGetValue(Key(variable), out var value).Should().BeTrue();

        return value.Value;
    }

    private Task<bool> Write(WiredInternalVariable variable, int value) =>
        _room.Harness.Room.WiredSystem.ApplyVariableMenuOperationAsync(
            new WiredVariableBinding(WiredVariableTargetType.Furni, CHAIR),
            variable.GetVarSnapshot().VariableId,
            WiredVariableMenuOperationType.SetValue,
            value,
            Ct
        );
}
