using FluentAssertions;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Wired.Variables.Furniture;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Habbo's inspection of a furni (official client capture, WIN63-202610121624,
/// WiredVariablesForObject for a Large Block) reports the held flags <c>@is_stackable</c> and
/// <c>@can_stand_on</c> as -1. Ours reported 1.
/// </summary>
public sealed class WiredHeldFlagValueTests
{
    private const int BLOCK = 20;

    private readonly WiredRoom _room = new(8, 8);

    [Fact]
    public void A_held_furni_flag_reads_minus_one()
    {
        _room.AddFloorItem(BLOCK, 1, 1);
        var stackable = new FurnitureIsStackableVariable(_room.Harness.Room);

        stackable
            .TryGetValue(
                new WiredVariableKey(
                    stackable.GetVarSnapshot().VariableId,
                    WiredVariableTargetType.Furni,
                    BLOCK
                ),
                out var value
            )
            .Should()
            .BeTrue();
        value.Value.Should().Be(-1);
    }
}
