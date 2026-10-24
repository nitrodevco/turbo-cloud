using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// W1 addon.21.E13: the Projectile add-on's time per tile is 1 to 100000 ms (AS3
/// WIN63-202610201032 <c>wired_setup/addons/_-s11.as:136</c>, the time per tile section
/// <c>createValueOrVariableSection(..., 1, 100000)</c>; confidential capture, the same range). A
/// projectile taking 0 ms a tile was saved.
/// </summary>
public sealed class WiredProjectileTimePerTileTests
{
    private readonly WiredRoom _room = new(8, 8);

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(100_000, true)]
    [InlineData(100_001, false)]
    public async Task Time_per_tile_runs_from_1_to_100000_ms(int timePerTile, bool saved)
    {
        _room.AddBox<WiredAddonProjectile>(1, 0, 0, "wf_xtra_rotate_to_dir");

        // The fresh box's values (official [1,1,0,0,100,-20,1,1,0,4,0,0,1,1,0,0,1,-20,50]) with
        // the time per tile changed.
        var result = await _room.SaveWithResultAsync<UpdateAddonMessage>(
            1,
            intParams:
            [
                1,
                (int)WiredDirectionalSystemType.EightDiffuse,
                0,
                0,
                timePerTile,
                -20,
                1,
                1,
                0,
                4,
                0,
                0,
                1,
                1,
                0,
                0,
                1,
                -20,
                50,
            ]
        );

        result.IsSaved.Should().Be(saved);
    }
}
