using System.Reflection;
using FluentAssertions;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Furniture.Wall;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A bought poster and a bought badge display, as a room shows them to the client: the item
/// carries the extra data the catalog wrote, and the room's logic for it reads that data the
/// way the client's room object does.
/// </summary>
public sealed class ProductFurniInRoomTests
{
    private readonly LiveRoomHarness _room = new();

    [Fact]
    public void APoster_IsSentWithItsId_SoTheClientDrawsThatPoster()
    {
        var poster = new RoomWallItem
        {
            ObjectId = 41,
            OwnerId = 1,
            OwnerName = "buyer",
            Definition = Definition(ProductType.Wall, FurnitureCategory.Poster, "default_wall"),
        };
        poster.SetExtraData(ProductStuffData.ExtraData("12"));

        EnsureLogic(poster);

        var packet = Turbo.Tests.Support.PacketHarness.Encode(poster.GetAddComposer());

        packet.PopString().Should().Be("41");
        packet.PopInt().Should().Be(4001);
        packet.PopString();
        packet.PopString().Should().Be("12", "the client draws poster12 from it");
    }

    [Fact]
    public void ABadgeDisplay_ShowsItsBadge_WhereTheClientReadsIt()
    {
        var display = new RoomFloorItem
        {
            ObjectId = 42,
            OwnerId = 1,
            OwnerName = "buyer",
            Definition = Definition(
                ProductType.Floor,
                FurnitureCategory.Default,
                BadgeDisplayData.LOGIC_NAME
            ),
        };
        display.SetExtraData(BadgeDisplayData.ExtraData("ACH_Login1", "buyer", "07-10-2026"));

        EnsureLogic(display);

        display
            .GetSnapshot()
            .StuffData.Should()
            .BeOfType<StringStuffSnapshot>()
            .Which.Data.Should()
            .Equal("0", "ACH_Login1", "buyer", "07-10-2026");
        display.Logic.GetUsagePolicy().Should().Be(FurnitureUsageType.Nobody);
    }

    private void EnsureLogic(object item) =>
        typeof(RoomObjectModule)
            .GetMethod("EnsureLogic", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(_room.Module<RoomObjectModule>(), [item])
            .Should()
            .Be(true);

    private static FurnitureDefinitionSnapshot Definition(
        ProductType type,
        FurnitureCategory category,
        string logic
    ) =>
        new()
        {
            Id = 4001,
            SpriteId = 4001,
            Name = "product_furni",
            ProductType = type,
            FurniCategory = category,
            LogicName = logic,
            TotalStates = 0,
            Width = 1,
            Length = 1,
            StackHeight = Altitude.Zero,
            CanStack = false,
            CanWalk = false,
            CanSit = false,
            CanLay = false,
            CanRecycle = false,
            CanTrade = true,
            CanGroup = false,
            CanSell = true,
            UsagePolicy = FurnitureUsageType.Nobody,
            ExtraData = null,
        };
}
