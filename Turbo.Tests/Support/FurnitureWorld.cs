using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Configuration;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Movements;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Database.Extensions;
using Turbo.Database.Movements;
using Turbo.Furniture.Providers;
using Turbo.Inventory;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Inventory.Factories;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Tests.Support;

/// <summary>
/// Players, rooms and furni definitions over one SQLite database, with the real recorder and
/// custody and real inventory grains over it: what the furniture custody tests share.
/// </summary>
public sealed class FurnitureWorld : IDisposable
{
    private static readonly BindingFlags All =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private readonly List<FurnitureDefinitionEntity> _definitions = [];

    public FurnitureWorld()
    {
        Fakes.Handlers["TryGetDefinition"] = call =>
            _definitions.FirstOrDefault(x => x.Id == (int)call.Args[0]!)?.ToSnapshot(0.01);
        Fakes.Handlers["GetPlayerNameAsync"] = _ => Task.FromResult<string?>("owner");
    }

    public SqliteDb Db { get; } = new();

    public Fakes Fakes { get; } = new();

    public TestOptionsMonitor<MovementsConfig> Config { get; } = new(new() { Enabled = true });

    public void Dispose() => Db.Dispose();

    /// <summary>The recorder as the host builds it, started, following <see cref="Config"/>.</summary>
    public async Task<AssetMovementRecorder> NewRecorderAsync(CancellationToken ct)
    {
        var recorder = new AssetMovementRecorder(
            Config,
            Db,
            NullLogger<AssetMovementRecorder>.Instance
        );
        await recorder.StartAsync(ct);

        return recorder;
    }

    /// <summary>
    /// A player's inventory grain, freshly activated, with its real furniture section and the
    /// custody over this database, recording through the real recorder.
    /// </summary>
    public async Task<IInventoryGrain> NewInventoryAsync(int playerId, CancellationToken ct)
    {
        var recorder = await NewRecorderAsync(ct);

        var grain = GrainHarness.Create(
            typeof(InventoryModule).Assembly,
            "Turbo.Inventory.Grains.InventoryGrain",
            Fakes,
            Db,
            playerId
        );
        var definitions = Fakes.Create<IFurnitureDefinitionProvider>();
        var loader = Activator.CreateInstance(
            typeof(InventoryModule).Assembly.GetType(
                "Turbo.Inventory.Factories.InventoryFurnitureLoader"
            )!,
            All,
            null,
            [
                Db,
                definitions,
                new StuffDataFactory(NullLogger<IStuffDataFactory>.Instance),
                NullLogger<IInventoryFurnitureLoader>.Instance,
            ],
            null
        )!;
        var module = Activator.CreateInstance(
            typeof(InventoryModule).Assembly.GetType(
                "Turbo.Inventory.Grains.Modules.InventoryFurniModule"
            )!,
            All,
            null,
            [
                grain,
                RoomHarness.GetMember(grain, "_state")!,
                TestCustody.Over(Db, recorder),
                loader,
                definitions,
                Fakes.Create<Turbo.Primitives.Catalog.ICatalogService>(),
                NullLogger.Instance,
            ],
            null
        )!;

        RoomHarness.SetField(grain, "FurniModule", module);

        return (IInventoryGrain)grain;
    }

    public void AddPlayer(int id, string name) =>
        Db.Insert(
            new PlayerEntity
            {
                Id = id,
                Name = name,
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );

    public void AddDefinition(
        int id,
        string name,
        string logic,
        ProductType productType = ProductType.Floor,
        FurnitureCategory category = FurnitureCategory.Default
    )
    {
        var definition = new FurnitureDefinitionEntity
        {
            Id = id,
            SpriteId = id,
            Name = name,
            ProductType = productType,
            FurniCategory = category,
            Logic = logic,
            Width = 1,
            Length = 1,
            StackHeight = 1,
            CanStack = true,
            CanWalk = false,
            CanSit = false,
            CanLay = false,
            CanRecycle = true,
            CanTrade = true,
            CanGroup = true,
            CanSell = true,
        };

        _definitions.Add(definition);
        Db.Insert(definition);
    }

    /// <summary>A room of <paramref name="ownerId"/>; the first one also adds the model rooms use.</summary>
    public void AddRoom(int id, int ownerId)
    {
        using (var db = Db.CreateDbContext())
        {
            if (!db.RoomModels.Any())
                Db.Insert(
                    new RoomModelEntity
                    {
                        Id = 1,
                        Name = "model",
                        Model = "00\r00",
                        DoorX = 0,
                        DoorY = 0,
                        DoorRotation = Rotation.North,
                        Enabled = true,
                        Custom = false,
                    }
                );
        }

        Db.Insert(
            new RoomEntity
            {
                Id = id,
                Name = "room" + id,
                PlayerEntityId = ownerId,
                RoomModelEntityId = 1,
                DoorMode = RoomDoorModeType.Open,
                UsersNow = 0,
                PlayersMax = 25,
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
    }

    /// <summary>A furni row of <paramref name="ownerId"/>, in <paramref name="roomId"/> or in no room.</summary>
    public int AddFurni(int id, int ownerId, int definitionId, int? roomId, int x = 1, int y = 1)
    {
        Db.Insert(
            new FurnitureEntity
            {
                Id = id,
                PlayerEntityId = ownerId,
                FurnitureDefinitionEntityId = definitionId,
                RoomEntityId = roomId,
                X = x,
                Y = y,
            }
        );

        return id;
    }

    public FurnitureEntity Row(int id)
    {
        using var db = Db.CreateDbContext();

        return db.Furnitures.AsNoTracking().Single(x => x.Id == id);
    }

    public List<FurnitureEntity> Rows()
    {
        using var db = Db.CreateDbContext();

        return db.Furnitures.AsNoTracking().OrderBy(x => x.Id).ToList();
    }

    public async Task DeleteRowAsync(int id, CancellationToken ct)
    {
        await using var db = await Db.CreateDbContextAsync(ct);

        await db.Furnitures.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
    }

    /// <summary>The recorded movements, oldest first, without the recording markers.</summary>
    public List<AssetMovementOutboxEntity> Movements()
    {
        using var db = Db.CreateDbContext();

        return db
            .AssetMovementOutbox.AsNoTracking()
            .Where(x =>
                x.Cause != MovementCauses.RECORDING_STARTED
                && x.Cause != MovementCauses.RECORDING_STOPPED
            )
            .OrderBy(x => x.Id)
            .ToList();
    }
}
