using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Networking;
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
    private const int DIMMER = 51;
    private const int OWNER = 1;

    private readonly LiveRoomHarness _room = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void A_moodlight_on_the_wall_gets_the_dimmer_logic()
    {
        var dimmer = Dimmer();

        var ensured = () => EnsureLogic(dimmer);

        ensured.Should().NotThrow().Which.Should().Be(true);
        dimmer.Logic.GetUsagePolicy().Should().Be(FurnitureUsageType.Controller);
    }

    /// <summary>
    /// Switching a moodlight on reaches the room as the whole wall item (ItemUpdate), as Habbo
    /// sends a used wall item (official client capture 2026-10-13: a wall item used twice came
    /// back as ItemUpdate with data "0" then "1"). The client's dimmer logic lights the room from
    /// that data update only; ItemDataUpdate feeds a post-it's text alone
    /// (<c>RoomEngine.as:4018</c>, <c>FurnitureRoomDimmerLogic.as:95</c>), so the switch did
    /// nothing (tester report 2026-10-16: "room dimmer doesnt work").
    /// </summary>
    [Fact]
    public async Task Switching_a_moodlight_on_tells_the_room_the_whole_wall_item()
    {
        var dimmer = Dimmer();
        EnsureLogic(dimmer);
        _room.State.ItemsById.Add((RoomObjectId)DIMMER, dimmer);

        (
            await _room.Room.InteractWithItemAsync(
                ActionContext.CreateForPlayer(OWNER, 1),
                DIMMER,
                new ToggleDimmerInteraction(),
                Ct
            )
        )
            .Should()
            .BeTrue();

        var sent = _room.Fakes.SentToRoom<IComposer>().ToList();

        sent.OfType<ItemDataUpdateMessageComposer>().Should().BeEmpty();
        sent.OfType<ItemUpdateMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.WallItem.StuffData.Should()
            .BeOfType<LegacyStuffSnapshot>()
            .Which.Data.Should()
            .Be("2,1,1,#000000,255");
    }

    /// <summary>
    /// A saved preset is read back from the item's extra data, which stores it camel-cased. It was
    /// read case-sensitively, so every read fell back to the defaults: switching the moodlight off
    /// wrote the default black preset into its data and the dialog reopened on defaults (user
    /// report 2026-10-17: blue, full brightness, background only, turned off, reopened - "the
    /// preset's data was gone").
    /// </summary>
    [Fact]
    public async Task A_saved_preset_survives_switching_the_moodlight_off()
    {
        var dimmer = Dimmer();
        EnsureLogic(dimmer);
        _room.State.ItemsById.Add((RoomObjectId)DIMMER, dimmer);
        var ctx = ActionContext.CreateForPlayer(OWNER, 1);

        (
            await _room.Room.InteractWithItemAsync(
                ctx,
                DIMMER,
                new SaveDimmerPresetInteraction
                {
                    PresetId = 2,
                    EffectType = 2,
                    Color = "#0053F7",
                    Brightness = 255,
                    Apply = true,
                },
                Ct
            )
        ).Should().BeTrue();
        (await _room.Room.InteractWithItemAsync(ctx, DIMMER, new ToggleDimmerInteraction(), Ct))
            .Should()
            .BeTrue();

        _room
            .Fakes.SentToRoom<ItemUpdateMessageComposer>()
            .Select(u => ((LegacyStuffSnapshot)u.WallItem.StuffData).Data)
            .Should()
            .Equal("2,2,2,#0053F7,255", "1,2,2,#0053F7,255");
    }

    private bool EnsureLogic(RoomWallItem dimmer) =>
        _room.Module<RoomObjectModule>().EnsureLogic(dimmer);

    private static RoomWallItem Dimmer()
    {
        var dimmer = new RoomWallItem
        {
            ObjectId = DIMMER,
            OwnerId = OWNER,
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

        return dimmer;
    }
}
