using FluentAssertions;
using Turbo.Rooms.Grains.Modules;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A floor plan the room will not take comes back as the error the client shows in its
/// <c>floorplan_editor.error</c> notification, written in the hotel's own texts. The import/export
/// dialog sends whatever was pasted into it, so these are reachable from the real client.
/// </summary>
public sealed class FloorPlanRefusalTests
{
    private readonly RoomMapModule _map = new LiveRoomHarness(10, 10).Module<RoomMapModule>();

    private static string Plan(int width, int height) =>
        string.Join('\r', Enumerable.Repeat(new string('0', width), height));

    [Fact]
    public async Task A_plan_over_the_tile_limit_is_refused_as_too_large_an_area()
    {
        // 63x64 is the plan pasted in the report: 62 * 63 = 3906 tiles past the 3025 limit.
        var error = await _map.SaveFloorPlanAsync(
            Plan(63, 64),
            null,
            false,
            CancellationToken.None
        );

        error
            .Should()
            .Be(
                "${notification.floorplan_editor.error.message.too_large_area} "
                    + "(${notification.floorplan_editor.error.message.max} 3025 "
                    + "${notification.floorplan_editor.error.message.tiles})"
            );
    }

    [Fact]
    public async Task A_plan_wider_than_an_axis_may_be_is_refused_as_too_wide()
    {
        var error = await _map.SaveFloorPlanAsync(Plan(65, 4), null, true, CancellationToken.None);

        error
            .Should()
            .Be(
                "${notification.floorplan_editor.error.message.too_large_width} "
                    + "(${notification.floorplan_editor.error.message.max} 64)"
            );
    }

    [Fact]
    public async Task A_plan_taller_than_an_axis_may_be_is_refused_as_too_tall()
    {
        var error = await _map.SaveFloorPlanAsync(Plan(4, 65), null, true, CancellationToken.None);

        error.Should().StartWith("${notification.floorplan_editor.error.message.too_large_height}");
    }

    [Fact]
    public async Task A_plan_with_a_character_that_is_no_height_is_refused_as_general()
    {
        var error = await _map.SaveFloorPlanAsync("00\r0!", null, false, CancellationToken.None);

        error.Should().Be("${notification.floorplan_editor.error.message.general}");
    }

    [Fact]
    public async Task A_plan_without_a_tile_under_the_door_is_refused_as_entry_not_on_tile()
    {
        var error = await _map.SaveFloorPlanAsync(
            "xxx\rx00\rx00",
            null,
            false,
            CancellationToken.None
        );

        error.Should().Be("${notification.floorplan_editor.error.message.entry_not_on_tile}");
    }
}
