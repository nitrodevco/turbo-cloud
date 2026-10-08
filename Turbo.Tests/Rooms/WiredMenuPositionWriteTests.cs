using System.Collections;
using FluentAssertions;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Wired.Variables.Furniture;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Wired Creator Tools' inspection tab writes a furni's <c>@position.x</c>: the furni moves, and
/// the room is told at once in the wired movement packet. It used to move on the server only, so
/// players saw it where it had been until they entered the room again.
/// </summary>
public sealed class WiredMenuPositionWriteTests
{
    private const int LAMP = 20;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Writing_position_x_from_the_inspection_tab_moves_the_furni_for_everyone()
    {
        var lamp = _room.AddFloorItem(LAMP, 2, 3);
        var variable = new FurniturePositionXVariable(_room.Harness.Room);
        var id = variable.GetVarSnapshot().VariableId;

        ((IDictionary)RoomHarness.GetMember(_room.Harness.Room.WiredSystem, "_variableById")!)[id] =
            variable;

        (
            await _room.Harness.Room.WiredSystem.ApplyVariableMenuOperationAsync(
                new WiredVariableBinding(WiredVariableTargetType.Furni, LAMP),
                id,
                WiredVariableMenuOperationType.SetValue,
                5,
                Ct
            )
        )
            .Should()
            .BeTrue();

        lamp.X.Should().Be(5);

        var moves = _room
            .Harness.Fakes.Log.Of("OnNextAsync")
            .Select(call => call.Args[0])
            .OfType<RoomOutboundSnapshot>()
            .SelectMany(x => x.Composers)
            .OfType<WiredMovementsMessageComposer>()
            .SelectMany(x => x.FloorItems)
            .ToList();

        moves.Should().ContainSingle(x => x.ObjectId == LAMP && x.TargetX == 5);
    }
}
