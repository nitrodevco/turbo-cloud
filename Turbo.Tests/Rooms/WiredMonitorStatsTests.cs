using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The wired monitor's furni counts are every floor and wall furni in the room, not only wired
/// boxes, against caps of 4000 each, and permanent variables are capped per kind (official client
/// capture of WiredRoomStats, WIN63-202610121624: "Floor furni 33/4000", "Wall furni 4/4000").
/// </summary>
public sealed class WiredMonitorStatsTests
{
    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_monitor_counts_every_furni_against_habbos_caps()
    {
        _room.AddFloorItem(30, 1, 1);
        _room.AddFloorItem(31, 2, 1);
        _room.AddFloorItem(32, 3, 1);
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");

        var stats = await _room.Harness.Room.GetWiredRoomStatsAsync(
            ActionContext.CreateForSystem(1),
            Ct
        );

        stats.Should().NotBeNull();
        stats!.FloorItemCount.Should().Be(4);
        stats.FloorItemCap.Should().Be(4000);
        stats.WallItemCap.Should().Be(4000);
        // One cap for every room by default (WiredConfig.ExecutionCostCapPerOwnerLevel 0).
        stats.ExecutionCostCap.Should().Be(4687.5);
        stats.MaxPermanentFurniVariables.Should().Be(23);
        stats.MaxPermanentUserVariables.Should().Be(93);
        stats.MaxPermanentGlobalVariables.Should().Be(70);
    }
}
