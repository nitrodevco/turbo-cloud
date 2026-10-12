using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Tests.Support;

/// <summary>
/// Furniture rows in a <see cref="SqliteDb"/>, which holds their foreign keys: the players who
/// own them, the rooms they stand in (owned by the first player) and one definition.
/// </summary>
public static class FurnitureRows
{
    public const int DEFINITION = 1;

    public static void InsertOwnersAndRooms(this SqliteDb db, int[] players, int[] rooms)
    {
        foreach (var id in players)
            db.Insert(
                new PlayerEntity
                {
                    Id = id,
                    Name = $"player-{id}",
                    Figure = "hd-180-1",
                    Gender = AvatarGenderType.Male,
                }
            );

        db.Insert(
            new RoomModelEntity
            {
                Id = 1,
                Name = "model_a",
                Model = "00\r00",
                DoorX = 0,
                DoorY = 0,
                DoorRotation = Rotation.North,
                Enabled = true,
                Custom = false,
            }
        );

        foreach (var id in rooms)
            db.Insert(
                new RoomEntity
                {
                    Id = id,
                    Name = $"room-{id}",
                    PlayerEntityId = players[0],
                    RoomModelEntityId = 1,
                    DoorMode = RoomDoorModeType.Open,
                    PlayersMax = 25,
                    WallHeight = -1,
                    ThicknessWall = RoomThicknessType.Normal,
                    ThicknessFloor = RoomThicknessType.Normal,
                    HideWalls = false,
                    AllowWalkThrough = true,
                    AllowPets = false,
                    AllowPetsEat = false,
                    TradeType = RoomTradeModeType.Disabled,
                    MuteType = ModSettingType.Owner,
                    KickType = ModSettingType.Owner,
                    BanType = ModSettingType.Owner,
                    ChatFloodType = ChatFloodSensitivityType.Minimal,
                    PlayerEntity = null!,
                    RoomModelEntity = null!,
                }
            );

        db.Insert(
            new FurnitureDefinitionEntity
            {
                Id = DEFINITION,
                SpriteId = 30,
                Name = "chair_norja",
                ProductType = ProductType.Floor,
                FurniCategory = FurnitureCategory.Default,
                Logic = "default_floor",
                Width = 1,
                Length = 1,
                StackHeight = 1,
                CanStack = false,
                CanWalk = false,
                CanSit = true,
                CanLay = false,
                CanRecycle = false,
                CanTrade = true,
                CanGroup = true,
                CanSell = true,
            }
        );
    }
}
