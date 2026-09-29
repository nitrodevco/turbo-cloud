using EvalHarness;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Object;
using Xunit;

namespace EvalHidden;

/// <summary>
/// Hidden regression tests: a click on furniture the room does not hold (a placement preview,
/// an item just picked up) is a no-op, not an error. Driven through the room grain's public
/// ClickItemByIdAsync, which is what RoomService calls for the client's click packet.
/// </summary>
public class StaleClickTests
{
    private static ActionContext Ctx() =>
        new()
        {
            Origin = ActionOrigin.Player,
            PlayerId = 1,
            RoomId = 1,
        };

    private static (RoomHarness, CapturingLogger<IRoomGrain>) Room()
    {
        var h = new RoomHarness();
        var log = new CapturingLogger<IRoomGrain>();
        RoomHarness.SetField(h.Room, "_logger", log);
        return (h, log);
    }

    [Fact]
    public async Task ClickOnAbsentItem_IsNoOp_AndNotLoggedAsError()
    {
        var (h, log) = Room();

        var result = await h.Room.ClickItemByIdAsync(Ctx(), 424242, CancellationToken.None);

        Assert.False(result);
        Assert.Empty(log.AtLeast(LogLevel.Error));
    }

    [Fact]
    public async Task ClickOnPickedUpItem_IsNoOp_AndNotLoggedAsError()
    {
        var (h, log) = Room();
        var item = h.CreateFloorItem(5, 2, 2, Altitude.Zero);
        h.AddToRoom(item);
        h.ItemsById.Remove(item.ObjectId); // picked up by someone else a moment ago

        var result = await h.Room.ClickItemByIdAsync(Ctx(), item.ObjectId, CancellationToken.None, 0);

        Assert.False(result);
        Assert.Empty(log.AtLeast(LogLevel.Error));
    }

    [Fact]
    public async Task ClickOnLiveItem_StillReachesIt()
    {
        var (h, log) = Room();
        var item = h.CreateFloorItem(6, 3, 3, Altitude.Zero);
        h.AddToRoom(item);

        var result = await h.Room.ClickItemByIdAsync(Ctx(), item.ObjectId, CancellationToken.None, 0);

        Assert.True(result);
        Assert.Empty(log.AtLeast(LogLevel.Error));
    }
}
