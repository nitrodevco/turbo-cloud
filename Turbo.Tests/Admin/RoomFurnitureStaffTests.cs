using FluentAssertions;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Rooms.Object;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// Staff taking an item out of a room from the panel's room page (2026-10-26: staff could not see
/// or move what stands in a room without going in): it goes back to its owner's inventory, as a
/// moderator picking it up in the room sends it, and an item not in the room is refused.
/// </summary>
public sealed class RoomFurnitureStaffTests
{
    private const int OWNER = 1;
    private const int STAFF = 99;
    private const int CHAIR = 20;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_item_taken_out_by_staff_goes_back_to_its_owner()
    {
        _room.AddFloorItem(CHAIR, 3, 3);
        _room.Harness.Fakes.Log.Calls.Clear();

        (await _room.Harness.Room.StaffPickupItemAsync(CHAIR, STAFF, Ct)).Should().BeTrue();

        _room.Harness.State.ItemsById.Should().NotContainKey(new RoomObjectId(CHAIR));
        _room
            .Harness.Fakes.Log.Calls.Where(x =>
                x.Interface == typeof(IInventoryGrain)
                && x.Method == nameof(IInventoryGrain.AddFurnitureFromRoomItemSnapshotAsync)
            )
            .Select(x => x.Key)
            .Should()
            .Equal((long)OWNER);
    }

    [Fact]
    public async Task An_item_not_in_the_room_is_refused()
    {
        (await _room.Harness.Room.StaffPickupItemAsync(404, STAFF, Ct)).Should().BeFalse();
    }
}
