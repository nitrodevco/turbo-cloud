using FluentAssertions;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Large Block (<c>bc_block_1*1</c>, height 1, five states, customparams -0.25): its asset's
/// logic is <c>furniture_multiheight</c> and each state draws it a quarter lower. Reported as
/// "lowering the block keeps its original height": the block looked lower and everything still
/// stood on it at height 1, since the server knew it only as plain furniture.
/// </summary>
public sealed class MultiHeightBlockTests
{
    private const int BLOCK = 40;

    private readonly WiredRoom _room = new();

    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 75)]
    [InlineData(2, 50)]
    [InlineData(4, 0)]
    public async Task Each_state_lowers_the_block_and_its_tile_a_quarter(int state, int height)
    {
        var block = AddBlock();

        await block.Logic.SetStateAsync(state);

        block.Logic.Should().BeOfType<FurnitureMultiHeightLogic>();
        block.GetStackHeight().ToInt().Should().Be(height);
        _room.Map.GetTileHeight(_room.Map.ToIdx(2, 2)).ToInt().Should().Be(height);
    }

    [Fact]
    public async Task The_room_is_sent_the_blocks_new_height()
    {
        var block = AddBlock();

        await block.Logic.SetStateAsync(2);

        _room
            .Harness.Fakes.Log.Of("OnNextAsync")
            .Select(call => call.Args[0])
            .OfType<RoomOutboundSnapshot>()
            .SelectMany(x => x.Composers)
            .OfType<ObjectUpdateMessageComposer>()
            .Should()
            .Contain(x => x.FloorItem.ObjectId == BLOCK && x.FloorItem.StackHeight.ToInt() == 50);
    }

    private IRoomFloorItem AddBlock()
    {
        var item = _room.AddFloorItem(BLOCK, 2, 2, "bc_block_1*1");

        item.GetType()
            .GetProperty(nameof(item.Definition))!
            .SetValue(
                item,
                item.Definition with
                {
                    LogicName = "default_floor",
                    TotalStates = 5,
                    StackHeight = Altitude.FromInt(100),
                    CustomParams = "-0.25",
                }
            );
        item.SetLogic(
            _room.Harness.LogicProvider.CreateLogicInstance(
                "default_floor",
                new RoomFloorItemContext(_room.Harness.Room, item)
            )
        );

        return item;
    }
}
