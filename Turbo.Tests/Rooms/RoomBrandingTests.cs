using System.Reflection;
using FluentAssertions;
using Turbo.Database.Migrations;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Habbo's room branding furni (WIN63-202610201032 <c>FurnitureRoomBrandingLogic</c>: map keys
/// <c>imageUrl</c>, <c>clickUrl</c>, <c>offsetX</c>, <c>offsetY</c>, <c>offsetZ</c>) and room
/// background toners. In the official client capture of room 78236752 an <c>ads_background</c>
/// arrives with map data <c>state=0, offsetX=-193, offsetY=183, offsetZ=8700,
/// imageUrl=.../infoa15_b.png</c>, usable with rights, and <c>room_info15_bg</c> with the int
/// data <c>[1, 134, 206, 159]</c>.
/// </summary>
public sealed class RoomBrandingTests
{
    private const int BACKGROUND = 7;
    private const string IMAGE_URL = "https://images.habbo.com/c_images/album3831/infoa15_b.png";

    private readonly WiredRoom _room = new();

    [Fact]
    public async Task Staff_branding_is_kept_as_map_data_and_survives_a_reload()
    {
        var item = _room.AddFloorItem(
            BACKGROUND,
            1,
            1,
            "room_branding",
            createLogic: (factory, ctx) => new FurnitureRoomBrandingLogic(factory, ctx),
            usagePolicy: FurnitureUsageType.Controller,
            totalStates: 0
        );

        (
            await _room.Harness.Room.SetObjectDataAsync(
                ActionContext.System,
                BACKGROUND,
                new Dictionary<string, string>
                {
                    ["offsetX"] = "-193",
                    ["offsetY"] = "183",
                    ["offsetZ"] = "8700",
                    ["imageUrl"] = IMAGE_URL,
                },
                CancellationToken.None
            )
        ).Should().BeTrue();

        item.SetExtraData(item.ExtraData.GetJsonString());
        var reloaded = new FurnitureRoomBrandingLogic(
            _room.StuffData,
            new RoomFloorItemContext(_room.Harness.Room, item)
        );

        reloaded
            .StuffData.Should()
            .BeAssignableTo<IMapStuffData>()
            .Which.Data.Should()
            .BeEquivalentTo(
                new Dictionary<string, string>
                {
                    ["state"] = "0",
                    ["offsetX"] = "-193",
                    ["offsetY"] = "183",
                    ["offsetZ"] = "8700",
                    ["imageUrl"] = IMAGE_URL,
                }
            );
        reloaded.GetUsagePolicy().Should().Be(FurnitureUsageType.Controller);
    }

    [Fact]
    public void The_branding_and_toner_furni_get_logics_the_room_registers()
    {
        var logics = ((string Name, string Logic)[])
            typeof(MapRoomBrandingAndTonerLogic)
                .GetField("LOGICS", BindingFlags.NonPublic | BindingFlags.Static)!
                .GetValue(null)!;
        var registered = typeof(FurnitureRoomBrandingLogic)
            .Assembly.GetTypes()
            .Select(x => x.GetCustomAttribute<RoomObjectLogicAttribute>()?.Key)
            .OfType<string>()
            .ToHashSet();

        logics
            .Should()
            .BeEquivalentTo(
                new[]
                {
                    ("ads_background", "room_branding"),
                    ("ads_mpu_160", "room_branding"),
                    ("ads_mpu_300", "room_branding"),
                    ("ads_mpu_720", "room_branding"),
                    ("roombg_color", "background_toner"),
                    ("room_info15_bg", "background_toner"),
                }
            );
        logics.Select(x => x.Logic).Should().OnlyContain(x => registered.Contains(x));
    }
}
