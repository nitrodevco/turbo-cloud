using System.Reflection;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Rooms.Configuration;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A room's wired usage cap is sized by its owner's account level as the room loads, and the
/// permanent variable caps scale with it: max(2500, 312.5 x (level + 2)) and floor(50 / 200 / 150
/// x cap / 10000) fit every official client capture of WiredRoomStats (levels 3 and 4: 2500 with
/// 12 / 50 / 37; level 7: 2812.5 with 14 / 56 / 42; level 13: 4687.5 with 23 / 93 / 70). Ours
/// gave every room the level 13 values, which stays the default while this hotel's account levels
/// are all the default one.
/// </summary>
public sealed class WiredUsageCapTests
{
    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(1, 2500, 12, 50, 37)]
    [InlineData(4, 2500, 12, 50, 37)]
    [InlineData(7, 2812.5, 14, 56, 42)]
    [InlineData(13, 4687.5, 23, 93, 70)]
    public async Task The_usage_cap_and_variable_caps_follow_the_owners_level(
        int level,
        double cap,
        int furni,
        int user,
        int global
    )
    {
        RoomHarness.SetField(
            _room.Harness.Room,
            "_wiredConfig",
            new WiredConfig { ExecutionCostCap = 2500, ExecutionCostCapPerOwnerLevel = 312.5 }
        );
        await LoadOwnerLevelAsync(level);

        var stats = await _room.Harness.Room.GetWiredRoomStatsAsync(
            ActionContext.CreateForSystem(1),
            Ct
        );

        stats!.ExecutionCostCap.Should().Be(cap);
        stats.MaxPermanentFurniVariables.Should().Be(furni);
        stats.MaxPermanentUserVariables.Should().Be(user);
        stats.MaxPermanentGlobalVariables.Should().Be(global);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    public async Task By_default_every_room_keeps_the_level_13_caps_whatever_its_owners_level(
        int level
    )
    {
        await LoadOwnerLevelAsync(level);

        var stats = await _room.Harness.Room.GetWiredRoomStatsAsync(
            ActionContext.CreateForSystem(1),
            Ct
        );

        stats!.ExecutionCostCap.Should().Be(4687.5);
        stats.MaxPermanentFurniVariables.Should().Be(23);
        stats.MaxPermanentUserVariables.Should().Be(93);
        stats.MaxPermanentGlobalVariables.Should().Be(70);
    }

    /// <summary>What the room does as it loads: read the owner's level and keep it.</summary>
    private async Task LoadOwnerLevelAsync(int level)
    {
        _room.Harness.Fakes.Handlers["GetSummaryAsync"] = _ =>
            Task.FromResult(
                Activator.CreateInstance<PlayerSummarySnapshot>() with
                {
                    AccountLevel = level,
                }
            );

        var read =
            (Task<int>)
                _room
                    .Harness.Room.GetType()
                    .GetMethod(
                        "GetOwnerAccountLevelAsync",
                        BindingFlags.Instance | BindingFlags.NonPublic
                    )!
                    .Invoke(_room.Harness.Room, [Ct])!;

        RoomHarness.SetMember(_room.Harness.State, "OwnerAccountLevel", await read);
    }
}
