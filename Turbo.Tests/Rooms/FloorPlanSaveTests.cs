using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Rooms.Grains.Modules;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A floor plan is written to the room's own model row, <c>room_&lt;id&gt;</c>. A room that is not on
/// that row while it exists (its plan was put back on a stock model, or the row was left from older
/// data) used to try to make a second <c>room_&lt;id&gt;</c>, which the unique name refuses: every
/// save failed and the player saw only "General".
/// </summary>
public sealed class FloorPlanSaveTests : IDisposable
{
    private const int OWNER = 1;
    private const int ROOM = 1;
    private const int STOCK_MODEL = 1;
    private const int OWN_MODEL = 2;

    private readonly LiveRoomHarness _room = new();
    private readonly SqliteDb _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public FloorPlanSaveTests()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = OWNER,
                Name = "owner",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
        _db.Insert(Model(STOCK_MODEL, "model_a", "00\r00", custom: false));
        _db.Insert(Model(OWN_MODEL, $"room_{ROOM}", "000\r000", custom: true));
        _db.Insert(
            new RoomEntity
            {
                Id = ROOM,
                Name = "room",
                PlayerEntityId = OWNER,
                RoomModelEntityId = STOCK_MODEL,
                DoorMode = RoomDoorModeType.Open,
                UsersNow = 0,
                PlayersMax = 25,
                PaintWall = "101",
                WallHeight = -1,
                HideWalls = false,
                ThicknessWall = RoomThicknessType.Normal,
                ThicknessFloor = RoomThicknessType.Normal,
                AllowBlocking = false,
                AllowPets = true,
                AllowPetsEat = true,
                TradeType = RoomTradeModeType.Disabled,
                MuteType = ModSettingType.Owner,
                KickType = ModSettingType.Owner,
                BanType = ModSettingType.Owner,
                ChatFloodType = ChatFloodSensitivityType.Minimal,
                PlayerEntity = null!,
                RoomModelEntity = null!,
            }
        );
        _room.Room.UseDatabase(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task A_plan_is_written_to_the_rooms_own_model_row_even_when_the_room_is_not_on_it()
    {
        await _room.Module<RoomMapModule>().SaveFloorPlanAsync("0000\r0000\r0000", null, false, Ct);

        await using var db = await _db.CreateDbContextAsync(Ct);
        var room = await db.Rooms.SingleAsync(x => x.Id == ROOM, Ct);
        var models = await db.RoomModels.Where(x => x.Name == $"room_{ROOM}").ToListAsync(Ct);

        room.RoomModelEntityId.Should().Be(OWN_MODEL);
        models.Should().ContainSingle().Which.Model.Should().Be("0000\r0000\r0000");
        (await db.RoomModels.SingleAsync(x => x.Id == STOCK_MODEL, Ct))
            .Model.Should()
            .Be("00\r00", "a stock model is never written through");
    }

    /// <summary>
    /// A wall item is pinned to the tile its wall stands on, outside the floor, so checking that
    /// tile for floor sent every poster, lamp and moodlight home on any save, even one that kept
    /// every wall where it was (tester report 2026-10-15).
    /// </summary>
    [Fact]
    public async Task Wall_items_stay_on_walls_the_new_plan_keeps_and_go_home_from_walls_it_drops()
    {
        var wired = new WiredRoom(6, 6);
        wired.Harness.Room.UseDatabase(_db);
        wired.Harness.Room.UseRoomModels(
            new Turbo.Rooms.Providers.RoomModelProvider(
                _db,
                Microsoft
                    .Extensions
                    .Logging
                    .Abstractions
                    .NullLogger<Turbo.Primitives.Rooms.Providers.IRoomModelProvider>
                    .Instance
            )
        );

        // On the left wall west of (1, 2), on the right wall north of (2, 1), and on a left
        // wall west of (4, 4), which the new plan no longer has a floor for.
        var left = Hang(wired.AddWallItem(1), 0, 2, Rotation.South);
        var right = Hang(wired.AddWallItem(2), 2, 0, Rotation.West);
        var dropped = Hang(wired.AddWallItem(3), 3, 4, Rotation.South);

        var error = await wired
            .Harness.Module<RoomMapModule>()
            .SaveFloorPlanAsync(
                "xxxx\rx000\rx000\rx000",
                new Turbo.Primitives.Rooms.Snapshots.Mapping.FloorPlanPropertiesSnapshot
                {
                    DoorX = 1,
                    DoorY = 1,
                    DoorRotation = 2,
                    // The room's own wall settings, so only the tiles change.
                    WallThickness = 0,
                    FloorThickness = 0,
                    FixedWallsHeight = 0,
                },
                false,
                Ct
            );

        error.Should().BeNull();
        var items = wired.Harness.State.ItemsById;
        items
            .ContainsKey(left.ObjectId)
            .Should()
            .BeTrue("its wall and the floor it faces are kept");
        items
            .ContainsKey(right.ObjectId)
            .Should()
            .BeTrue("its wall and the floor it faces are kept");
        items.ContainsKey(dropped.ObjectId).Should().BeFalse("the floor it faced is gone");
    }

    private static Turbo.Rooms.Object.Furniture.Wall.RoomWallItem Hang(
        Turbo.Rooms.Object.Furniture.Wall.RoomWallItem item,
        int x,
        int y,
        Rotation rotation
    )
    {
        item.SetPosition(x, y);
        item.SetRotation(rotation);

        return item;
    }

    private static RoomModelEntity Model(int id, string name, string model, bool custom) =>
        new()
        {
            Id = id,
            Name = name,
            Model = model,
            DoorX = 0,
            DoorY = 0,
            DoorRotation = Rotation.North,
            Enabled = true,
            Custom = custom,
        };
}
