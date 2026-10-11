using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Database;

/// <summary>
/// What a delete takes with it. A definition (a furni, a room model, a currency) can't be deleted
/// while anything uses it: a cascade took every copy of a furni out of every inventory and room,
/// every room built on a model, and every balance in a currency. A deleted room's chat and entry
/// logs stay, as moderation evidence, and nobody keeps it as a home room.
/// </summary>
public sealed class DeleteRulesTests : IDisposable
{
    private const int OWNER = 1;
    private const int ROOM = 1;
    private const int MODEL = 1;
    private const int DEFINITION = 1;
    private const int CURRENCY = 1;

    private readonly SqliteDb _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public DeleteRulesTests()
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
        _db.Insert(
            new RoomModelEntity
            {
                Id = MODEL,
                Name = "model_a",
                Model = "00\r00",
                DoorX = 0,
                DoorY = 0,
                DoorRotation = Rotation.North,
                Enabled = true,
                Custom = false,
            }
        );
        _db.Insert(Room());
        _db.Insert(
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
        _db.Insert(
            new CurrencyTypeEntity
            {
                Id = CURRENCY,
                Name = "duckets",
                CurrencyType = CurrencyType.ActivityPoints,
                ActivityPointType = 0,
                Enabled = true,
            }
        );
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task A_furni_definition_someone_owns_cant_be_deleted()
    {
        _db.Insert(
            new FurnitureEntity
            {
                Id = 1,
                PlayerEntityId = OWNER,
                FurnitureDefinitionEntityId = DEFINITION,
            }
        );

        await DeletingRefused(db => db.FurnitureDefinitions.Where(x => x.Id == DEFINITION));
        (await Count(db => db.Furnitures)).Should().Be(1);
    }

    [Fact]
    public async Task A_room_model_a_room_is_built_on_cant_be_deleted()
    {
        await DeletingRefused(db => db.RoomModels.Where(x => x.Id == MODEL));
        (await Count(db => db.Rooms)).Should().Be(1);
    }

    [Fact]
    public async Task A_currency_someone_holds_cant_be_deleted()
    {
        _db.Insert(
            new PlayerCurrencyEntity
            {
                Id = 1,
                PlayerEntityId = OWNER,
                CurrencyTypeEntityId = CURRENCY,
                Amount = 50,
            }
        );

        await DeletingRefused(db => db.CurrencyTypes.Where(x => x.Id == CURRENCY));
        (await Count(db => db.PlayerCurrencies)).Should().Be(1);
    }

    [Fact]
    public async Task A_deleted_rooms_chat_and_visits_stay_and_it_is_nobodys_home_room()
    {
        _db.Insert(
            new RoomChatlogEntity
            {
                Id = 1,
                RoomEntityId = ROOM,
                PlayerEntityId = OWNER,
                Message = "evidence",
            }
        );
        _db.Insert(
            new RoomEntryLogEntity
            {
                Id = 1,
                RoomEntityId = ROOM,
                PlayerEntityId = OWNER,
            }
        );
        _db.Insert(
            new PlayerSettingsEntity
            {
                Id = 1,
                PlayerEntityId = OWNER,
                HomeRoomId = ROOM,
            }
        );

        var room = new LiveRoomHarness();
        RoomHarness.SetField(room.Room, "_dbCtxFactory", _db);
        var owner = ActionContext.CreateForPlayer(OWNER, ROOM);

        (await room.Room.PrepareRoomDeletionAsync(owner, Ct)).Should().NotBeNull();
        await room.Room.CompleteRoomDeletionAsync(Ct);

        await using var db = await _db.CreateDbContextAsync(Ct);

        (await db.Rooms.AnyAsync(Ct)).Should().BeFalse();
        (await db.Chatlogs.SingleAsync(Ct)).Message.Should().Be("evidence");
        (await db.RoomEntryLogs.CountAsync(Ct)).Should().Be(1);
        (await db.PlayerSettings.SingleAsync(Ct)).HomeRoomId.Should().BeNull();
    }

    private async Task DeletingRefused<T>(
        Func<Turbo.Database.Context.TurboDbContext, IQueryable<T>> rows
    )
    {
        await using var db = await _db.CreateDbContextAsync(Ct);

        var act = () => rows(db).ExecuteDeleteAsync(Ct);

        await act.Should().ThrowAsync<Exception>("the database refuses it while it is used");
    }

    private async Task<int> Count<T>(
        Func<Turbo.Database.Context.TurboDbContext, IQueryable<T>> rows
    )
    {
        await using var db = await _db.CreateDbContextAsync(Ct);

        return await rows(db).CountAsync(Ct);
    }

    private static RoomEntity Room() =>
        new()
        {
            Id = ROOM,
            Name = "room",
            PlayerEntityId = OWNER,
            RoomModelEntityId = MODEL,
            DoorMode = RoomDoorModeType.Open,
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
        };
}
