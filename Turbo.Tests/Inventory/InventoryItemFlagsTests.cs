using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Inventory.Furni;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Inventory;

public class InventoryItemFlagsTests
{
    /// <summary>
    /// Official client captures of the inventory (FurniList / FurniListAddOrUpdate, 69 kinds,
    /// 2026-10-13..17) send an item as recyclable only when its furnidata says recyclable and
    /// it is tradeable: chair_nt_polyfon, usva_nt_shelf, bed_silo_two_notrd, joulutahti_notrd and
    /// guild_forum are recyclable but untradeable and arrive with recyclable false.
    /// </summary>
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void An_item_is_listed_as_recyclable_only_when_it_is_also_tradeable(
        bool canRecycle,
        bool canTrade,
        bool listedRecyclable
    )
    {
        var packet = PacketHarness.Encode(
            new FurniListAddOrUpdateEventMessageComposer { Items = [Item(canRecycle, canTrade)] }
        );
        Assert.Equal(1, packet.PopInt()); // items
        _ = packet.PopInt(); // id
        _ = packet.PopString(); // type
        _ = packet.PopInt(); // ref
        _ = packet.PopInt(); // sprite
        _ = packet.PopInt(); // category
        Assert.Equal((int)StuffDataType.LegacyKey, packet.PopInt()); // stuff data: legacy, no flags
        Assert.Equal("0", packet.PopString());

        Assert.Equal(listedRecyclable, packet.PopBoolean());
        Assert.Equal(canTrade, packet.PopBoolean());
    }

    private static FurnitureItemSnapshot Item(bool canRecycle, bool canTrade) =>
        new()
        {
            ItemId = 501,
            SpriteId = 4352,
            OwnerId = 1,
            OwnerName = "owner",
            Definition = new FurnitureDefinitionSnapshot
            {
                Id = 9,
                SpriteId = 4352,
                Name = "bed_silo_two_notrd",
                ProductType = ProductType.Floor,
                FurniCategory = FurnitureCategory.Default,
                LogicName = "default",
                TotalStates = 1,
                Width = 1,
                Length = 1,
                StackHeight = 1,
                CanStack = true,
                CanWalk = false,
                CanSit = false,
                CanLay = false,
                CanRecycle = canRecycle,
                CanTrade = canTrade,
                CanGroup = true,
                CanSell = true,
                UsagePolicy = FurnitureUsageType.Everybody,
                ExtraData = null,
            },
            StuffData = new LegacyStuffSnapshot { StuffBitmask = 0, Data = "0" },
            ExtraData = string.Empty,
            SecondsToExpiration = -1,
            HasRentPeriodStarted = false,
            RoomId = 0,
            SlotId = string.Empty,
            Extra = 0,
        };
}
