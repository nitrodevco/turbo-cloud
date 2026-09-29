using EvalHarness;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Rooms.Grains.Modules;
using Xunit;

namespace EvalHidden;

/// <summary>
/// Hidden regression tests for "rotating the bottom item of a stack". Driven through
/// RoomFurniModule.MoveFloorItemAsync, the one path both the client's move/rotate and wired's
/// move/rotate take, so a fix made in the furni module or in the map module both count.
/// </summary>
public class RotateRestackTests
{
    private static readonly Altitude One = Altitude.FromValue(1.0);

    private static ActionContext Ctx() =>
        new()
        {
            Origin = ActionOrigin.Player,
            PlayerId = 1,
            RoomId = 1,
        };

    private static async Task Move(RoomHarness h, IRoomFloorItemLike item, int x, int y, Rotation? rot, Altitude? z = null)
    {
        var furni = h.Module<RoomFurniModule>();
        var map = h.Module<RoomMapModule>();
        var ok = await furni.MoveFloorItemAsync(Ctx(), item.Item, map.ToIdx(x, y), z, rot, false, CancellationToken.None);
        Assert.True(ok, "the move itself should be accepted");
    }

    [Fact]
    public async Task RotatingBottomItemInPlace_LandsOnTopOfTheStack()
    {
        var h = new RoomHarness();
        var bottom = h.CreateFloorItem(1, 2, 2, Altitude.Zero, Rotation.North, One);
        h.AddToRoom(bottom);
        var top = h.CreateFloorItem(2, 2, 2, One, Rotation.North, One);
        h.AddToRoom(top);

        await Move(h, new(bottom), 2, 2, Rotation.East);

        Assert.Equal(Rotation.East, bottom.Rotation);
        Assert.Equal(2.0, bottom.Z.Value, 3);
    }

    [Fact]
    public async Task RotatingAgain_KeepsClimbing()
    {
        var h = new RoomHarness();
        var a = h.CreateFloorItem(1, 3, 3, Altitude.Zero, Rotation.North, One);
        h.AddToRoom(a);
        var b = h.CreateFloorItem(2, 3, 3, One, Rotation.North, One);
        h.AddToRoom(b);

        await Move(h, new(a), 3, 3, Rotation.East); // a -> 2.0 (on b)
        await Move(h, new(b), 3, 3, Rotation.East); // b -> 3.0 (on a)

        Assert.Equal(2.0, a.Z.Value, 3);
        Assert.Equal(3.0, b.Z.Value, 3);
    }

    [Fact]
    public async Task RotatingLoneItemInPlace_KeepsFloorHeight()
    {
        var h = new RoomHarness();
        var lone = h.CreateFloorItem(1, 4, 4, Altitude.Zero, Rotation.North, One);
        h.AddToRoom(lone);

        await Move(h, new(lone), 4, 4, Rotation.South);

        Assert.Equal(0.0, lone.Z.Value, 3);
    }

    [Fact]
    public async Task RotatingTopItemInPlace_StaysOnWhatIsUnderIt()
    {
        var h = new RoomHarness();
        var bottom = h.CreateFloorItem(1, 2, 5, Altitude.Zero, Rotation.North, One);
        h.AddToRoom(bottom);
        var top = h.CreateFloorItem(2, 2, 5, One, Rotation.North, One);
        h.AddToRoom(top);

        await Move(h, new(top), 2, 5, Rotation.West);

        Assert.Equal(1.0, top.Z.Value, 3);
        Assert.Equal(0.0, bottom.Z.Value, 3);
    }

    [Fact]
    public async Task NoChange_KeepsHeight()
    {
        var h = new RoomHarness();
        var bottom = h.CreateFloorItem(1, 6, 6, Altitude.Zero, Rotation.North, One);
        h.AddToRoom(bottom);
        var top = h.CreateFloorItem(2, 6, 6, One, Rotation.North, One);
        h.AddToRoom(top);

        await Move(h, new(bottom), 6, 6, Rotation.North);

        Assert.Equal(0.0, bottom.Z.Value, 3);
    }

    [Fact]
    public async Task ExplicitHeight_IsRespectedWhenRotatingInPlace()
    {
        var h = new RoomHarness();
        var bottom = h.CreateFloorItem(1, 1, 6, Altitude.Zero, Rotation.North, One);
        h.AddToRoom(bottom);
        var top = h.CreateFloorItem(2, 1, 6, One, Rotation.North, One);
        h.AddToRoom(top);

        await Move(h, new(bottom), 1, 6, Rotation.East, Altitude.FromValue(0.5));

        Assert.Equal(0.5, bottom.Z.Value, 3);
    }

    [Fact]
    public async Task MovingToAnotherTile_LandsOnThatTile()
    {
        var h = new RoomHarness();
        var bottom = h.CreateFloorItem(1, 2, 2, Altitude.Zero, Rotation.North, One);
        h.AddToRoom(bottom);
        var top = h.CreateFloorItem(2, 2, 2, One, Rotation.North, One);
        h.AddToRoom(top);

        await Move(h, new(bottom), 5, 1, Rotation.North);

        Assert.Equal(0.0, bottom.Z.Value, 3);
    }
}

/// <summary>Adapter so the helper takes whatever floor-item interface the base exposes.</summary>
public readonly record struct IRoomFloorItemLike(Turbo.Primitives.Rooms.Object.Furniture.Floor.IRoomFloorItem Item);
