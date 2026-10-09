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
        RoomHarness.SetField(_room.Room, "_dbCtxFactory", _db);
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
