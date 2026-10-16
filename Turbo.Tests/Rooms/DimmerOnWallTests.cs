using System.Reflection;
using FluentAssertions;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Furniture.Wall;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Every moodlight Habbo has (roomdimmer, dimmer_swtch, dimmer_buttn, dimmer_fuse*) is a wall
/// item: furnidata lists them under <c>wallitemtypes</c>, category <c>dimmer</c>. The dimmer logic
/// took a floor item's context, so a wall item with it got no logic at all and could not be
/// placed (tester report 2026-10-15: "now i also cant place a mood switch").
/// </summary>
public sealed class DimmerOnWallTests
{
    private readonly LiveRoomHarness _room = new();

    [Fact]
    public void A_moodlight_on_the_wall_gets_the_dimmer_logic()
    {
        var dimmer = new RoomWallItem
        {
            ObjectId = 51,
            OwnerId = 1,
            OwnerName = "owner",
            Definition = new FurnitureDefinitionSnapshot
            {
                Id = 4027,
                SpriteId = 4027,
                Name = "roomdimmer",
                ProductType = ProductType.Wall,
                FurniCategory = FurnitureCategory.Default,
                LogicName = "dimmer",
                TotalStates = 0,
                Width = 1,
                Length = 1,
                StackHeight = Altitude.Zero,
                CanStack = false,
                CanWalk = false,
                CanSit = false,
                CanLay = false,
                CanRecycle = true,
                CanTrade = true,
                CanGroup = false,
                CanSell = true,
                UsagePolicy = FurnitureUsageType.Controller,
                ExtraData = null,
            },
        };
        dimmer.SetExtraData(null);

        var ensured = () =>
            typeof(RoomObjectModule)
                .GetMethod("EnsureLogic", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(_room.Module<RoomObjectModule>(), [dimmer]);

        ensured.Should().NotThrow().Which.Should().Be(true);
        dimmer.Logic.GetUsagePolicy().Should().Be(FurnitureUsageType.Controller);
    }
}
