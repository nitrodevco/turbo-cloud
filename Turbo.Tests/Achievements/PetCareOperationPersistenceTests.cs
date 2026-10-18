using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Achievements;
using Turbo.Database.Achievements;
using Turbo.Database.Entities.Achievements;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Pets;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Database.Extensions;
using Turbo.Inventory;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Pets.Providers;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Rooms.Grains;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Achievements;

public sealed class PetCareOperationPersistenceTests : IDisposable
{
    private const int ROOM_ID = 71;
    private const int ACTOR_ID = 101;
    private const int OWNER_ID = 202;
    private const int PET_ID = 303;
    private readonly SqliteDb _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PetCareOperationPersistenceTests()
    {
        _db.Insert(NewPlayer(ACTOR_ID, "pet-care-actor"));
        _db.Insert(NewPlayer(OWNER_ID, "pet-care-owner"));
        _db.Insert(
            new RoomModelEntity
            {
                Id = 1,
                Name = "pet-care-model",
                Model = "00\r00",
                DoorX = 0,
                DoorY = 0,
                DoorRotation = Rotation.North,
                Enabled = true,
                Custom = false,
            }
        );
        _db.Insert(
            new RoomEntity
            {
                Id = ROOM_ID,
                Name = "pet-care-room",
                PlayerEntityId = OWNER_ID,
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
        _db.Insert(NewPet());
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task NutritionOperationCommitsOnlyCappedGainAndReplaysWithoutDuplicateFact()
    {
        var persistence = CreatePersistenceGrain().Persistence;

        var first = await persistence.ApplyPetNutritionOperationAsync(
            "bowl-feed-1",
            PET_ID,
            OWNER_ID,
            OWNER_ID,
            baseNutrition: 95,
            requestedNutrition: 20,
            maxNutrition: 100,
            Ct
        );
        var replay = await persistence.ApplyPetNutritionOperationAsync(
            "bowl-feed-1",
            PET_ID,
            OWNER_ID,
            OWNER_ID,
            baseNutrition: 95,
            requestedNutrition: 20,
            maxNutrition: 100,
            Ct
        );

        first.Should().Be(new PetNutritionOperationResult { Nutrition = 100, ActualGain = 5 });
        replay.Should().Be(first);
        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.Pets.SingleAsync(Ct)).Nutrition.Should().Be(100);
        var operation = await db.PetNutritionOperations.SingleAsync(Ct);
        operation.ActualGain.Should().Be(5);
        operation.Completed.Should().BeTrue();
        var facts = await db.AchievementFacts.ToListAsync(Ct);
        facts.Should().ContainSingle();
        using var json = JsonDocument.Parse(facts[0].FactJson);
        facts[0].PlayerId.Should().Be(OWNER_ID);
        json.RootElement.GetProperty("Amount").GetInt64().Should().Be(5);
        json.RootElement.GetProperty("Source")
            .GetString()
            .Should()
            .Be(AchievementSources.NUTRITION);
    }

    [Fact]
    public async Task NutritionAtTheCapIsDurableButDoesNotCountAnAttemptAsNutrition()
    {
        var persistence = CreatePersistenceGrain().Persistence;

        var result = await persistence.ApplyPetNutritionOperationAsync(
            "bowl-feed-full",
            PET_ID,
            OWNER_ID,
            OWNER_ID,
            baseNutrition: 100,
            requestedNutrition: 20,
            maxNutrition: 100,
            Ct
        );

        result.Should().Be(new PetNutritionOperationResult { Nutrition = 100, ActualGain = 0 });
        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.PetNutritionOperations.CountAsync(Ct)).Should().Be(1);
        (await db.AchievementFacts.CountAsync(Ct)).Should().Be(0);
    }

    [Fact]
    public async Task NutritionCanBeRecordedAgainAfterDecayReturnsToAnEarlierBase()
    {
        var persistence = CreatePersistenceGrain().Persistence;
        var first = await persistence.ApplyPetNutritionOperationAsync(
            "feed-action-1",
            PET_ID,
            OWNER_ID,
            OWNER_ID,
            baseNutrition: 95,
            requestedNutrition: 10,
            maxNutrition: 100,
            Ct
        );
        first.ActualGain.Should().Be(5);

        await using (var db = await _db.CreateDbContextAsync(Ct))
        {
            var pet = await db.Pets.SingleAsync(Ct);
            pet.Nutrition = 95;
            await db.SaveChangesAsync(Ct);
        }

        var second = await persistence.ApplyPetNutritionOperationAsync(
            "feed-action-2",
            PET_ID,
            OWNER_ID,
            OWNER_ID,
            baseNutrition: 95,
            requestedNutrition: 10,
            maxNutrition: 100,
            Ct
        );

        second.Should().Be(new PetNutritionOperationResult { Nutrition = 100, ActualGain = 5 });
        await using var check = await _db.CreateDbContextAsync(Ct);
        (await check.PetNutritionOperations.CountAsync(Ct)).Should().Be(2);
        (await check.AchievementFacts.CountAsync(Ct)).Should().Be(2);
    }

    [Fact]
    public async Task PetRespectPersistsPetAndPairedFactsOnceAndReplaysItsResult()
    {
        var (persistence, fakes) = CreatePersistenceGrain(acceptPetRespect: true);

        var first = await persistence.ApplyPetRespectOperationAsync(
            "pet-respect-1",
            ACTOR_ID,
            PET_ID,
            OWNER_ID,
            baseRespect: 4,
            Ct
        );
        var replay = await persistence.ApplyPetRespectOperationAsync(
            "pet-respect-1",
            ACTOR_ID,
            PET_ID,
            OWNER_ID,
            baseRespect: 4,
            Ct
        );

        first.Should().Be(new PetRespectOperationResult { Accepted = true, Respect = 5 });
        replay.Should().Be(first);
        fakes.Log.Of(nameof(IPlayerGrain.SpendPetRespectOperationAsync)).Should().ContainSingle();
        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.Pets.SingleAsync(Ct)).Respect.Should().Be(5);
        (await db.PetRespectOperations.SingleAsync(Ct)).Completed.Should().BeTrue();
        var facts = await db.AchievementFacts.OrderBy(x => x.PlayerId).ToListAsync(Ct);
        var factSources = new List<(int PlayerId, string? Source)>();
        foreach (var fact in facts)
        {
            using var json = JsonDocument.Parse(fact.FactJson);
            factSources.Add((fact.PlayerId, json.RootElement.GetProperty("Source").GetString()));
        }
        factSources
            .Should()
            .Equal(
                (ACTOR_ID, AchievementSources.PET_RESPECT_GIVEN),
                (OWNER_ID, AchievementSources.PET_RESPECT_RECEIVED)
            );
    }

    [Fact]
    public async Task RejectedPetRespectHasNoPetMutationOrAchievementFacts()
    {
        var (persistence, _) = CreatePersistenceGrain(acceptPetRespect: false);

        var result = await persistence.ApplyPetRespectOperationAsync(
            "pet-respect-rejected",
            ACTOR_ID,
            PET_ID,
            OWNER_ID,
            baseRespect: 4,
            Ct
        );

        result.Accepted.Should().BeFalse();
        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.Pets.SingleAsync(Ct)).Respect.Should().Be(0);
        (await db.PetRespectOperations.SingleAsync(Ct)).Rejected.Should().BeTrue();
        (await db.AchievementFacts.CountAsync(Ct)).Should().Be(0);
    }

    [Fact]
    public async Task RejectedPetRespectDoesNotBlockALaterDistinctAction()
    {
        var allowed = false;
        var fakes = new Fakes();
        fakes.Handlers[nameof(IPlayerGrain.SpendPetRespectOperationAsync)] = _ =>
            Task.FromResult(allowed);
        var persistence = CreatePersistenceGrain(fakes);

        var rejected = await persistence.ApplyPetRespectOperationAsync(
            "respect-action-rejected",
            ACTOR_ID,
            PET_ID,
            OWNER_ID,
            baseRespect: 0,
            Ct
        );
        allowed = true;
        var accepted = await persistence.ApplyPetRespectOperationAsync(
            "respect-action-next-day",
            ACTOR_ID,
            PET_ID,
            OWNER_ID,
            baseRespect: 0,
            Ct
        );

        rejected.Accepted.Should().BeFalse();
        accepted.Should().Be(new PetRespectOperationResult { Accepted = true, Respect = 1 });
        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.PetRespectOperations.CountAsync(Ct)).Should().Be(2);
        (await db.AchievementFacts.CountAsync(Ct)).Should().Be(2);
    }

    [Fact]
    public async Task PetRespectAddsToTheLatestPersistedRespectInsteadOfAnOldActionSnapshot()
    {
        var persistence = CreatePersistenceGrain(acceptPetRespect: true).Persistence;
        await using (var db = await _db.CreateDbContextAsync(Ct))
        {
            var pet = await db.Pets.SingleAsync(Ct);
            pet.Respect = 7;
            await db.SaveChangesAsync(Ct);
        }

        var result = await persistence.ApplyPetRespectOperationAsync(
            "respect-action-stale-base",
            ACTOR_ID,
            PET_ID,
            OWNER_ID,
            baseRespect: 4,
            Ct
        );

        result.Should().Be(new PetRespectOperationResult { Accepted = true, Respect = 8 });
        await using var check = await _db.CreateDbContextAsync(Ct);
        (await check.Pets.SingleAsync(Ct)).Respect.Should().Be(8);
    }

    [Fact]
    public async Task PetRespectOperationResumesAfterActorReceiptWhenPetWasTemporarilyUnavailable()
    {
        var (persistence, fakes) = CreatePersistenceGrain(acceptPetRespect: true);
        await using (var db = await _db.CreateDbContextAsync(Ct))
            await db.Pets.Where(x => x.Id == PET_ID).ExecuteDeleteAsync(Ct);

        var pending = () =>
            persistence.ApplyPetRespectOperationAsync(
                "pet-respect-retry",
                ACTOR_ID,
                PET_ID,
                OWNER_ID,
                baseRespect: 4,
                Ct
            );
        await pending.Should().ThrowAsync<InvalidOperationException>();
        await using (var db = await _db.CreateDbContextAsync(Ct))
        {
            db.Pets.Add(NewPet());
            await db.SaveChangesAsync(Ct);
        }

        var result = await persistence.ApplyPetRespectOperationAsync(
            "pet-respect-retry",
            ACTOR_ID,
            PET_ID,
            OWNER_ID,
            baseRespect: 4,
            Ct
        );

        result.Should().Be(new PetRespectOperationResult { Accepted = true, Respect = 5 });
        fakes.Log.Of(nameof(IPlayerGrain.SpendPetRespectOperationAsync)).Should().HaveCount(2);
        await using var check = await _db.CreateDbContextAsync(Ct);
        (await check.AchievementFacts.CountAsync(Ct)).Should().Be(2);
    }

    private (IRoomPersistenceGrain Persistence, Fakes Fakes) CreatePersistenceGrain(
        bool acceptPetRespect = true
    )
    {
        var fakes = new Fakes();
        fakes.Handlers[nameof(IPlayerGrain.SpendPetRespectOperationAsync)] = _ =>
            Task.FromResult(acceptPetRespect);
        return (CreatePersistenceGrain(fakes), fakes);
    }

    [Fact]
    public async Task StaleQueuedSnapshotCannotEraseRecoveredPetRespect()
    {
        PetSnapshot stale;
        await using (var db = await _db.CreateDbContextAsync(Ct))
        {
            var pet = await db.Pets.SingleAsync(Ct);
            pet.RoomEntityId = ROOM_ID;
            pet.Level = 1;
            await db.SaveChangesAsync(Ct);
            stale = pet.ToSnapshot("owner") with { Level = 2 };
        }
        var (persistence, _) = CreatePersistenceGrain();
        await persistence.ApplyPetRespectOperationAsync(
            "recovered-before-stale-snapshot",
            ACTOR_ID,
            PET_ID,
            OWNER_ID,
            4,
            Ct
        );
        await persistence.EnqueueDirtyPetsAsync([stale], Ct);
        var flush =
            (Task<bool>)
                persistence
                    .GetType()
                    .GetMethod(
                        "FlushDirtyPetBatchAsync",
                        BindingFlags.Instance | BindingFlags.NonPublic
                    )!
                    .Invoke(persistence, [Ct])!;
        Assert.True(await flush);
        await using var after = await _db.CreateDbContextAsync(Ct);
        Assert.Equal(5, (await after.Pets.SingleAsync(Ct)).Respect);
        Assert.Equal(2, (await after.Pets.SingleAsync(Ct)).Level);
    }

    [Fact]
    public async Task ReturningLeveledPetBeforeRoomFlushRecordsDeltaOnceAndPreservesRespect()
    {
        PetSnapshot snapshot;
        await using (var db = await _db.CreateDbContextAsync(Ct))
        {
            var pet = await db.Pets.SingleAsync(Ct);
            pet.RoomEntityId = ROOM_ID;
            pet.Level = 1;
            pet.Respect = 7;
            await db.SaveChangesAsync(Ct);
            snapshot = pet.ToSnapshot("owner") with { Level = 3, Respect = 0 };
        }
        var inventory = CreateInventory();
        Assert.True(await inventory.ReturnPetAsync(snapshot, Ct));
        Assert.True(await inventory.ReturnPetAsync(snapshot, Ct));
        await using var after = await _db.CreateDbContextAsync(Ct);
        var returned = await after.Pets.SingleAsync(Ct);
        Assert.Null(returned.RoomEntityId);
        Assert.Equal(3, returned.Level);
        Assert.Equal(7, returned.Respect);
        var fact = await after.AchievementFacts.SingleAsync(Ct);
        Assert.Equal(AchievementSources.PET_LEVEL, fact.Source);
        using var json = JsonDocument.Parse(fact.FactJson);
        Assert.Equal(2, json.RootElement.GetProperty("Amount").GetInt64());
    }

    [Fact]
    public async Task PetDeletionWaitsForAdmittedRespectToComplete()
    {
        _db.Insert(
            new PetRespectOperationEntity
            {
                OperationId = "pending-before-delete",
                RoomId = ROOM_ID,
                ActorId = ACTOR_ID,
                PetId = PET_ID,
                OwnerId = OWNER_ID,
            }
        );
        var inventory = CreateInventory();
        Assert.False(await inventory.DeletePetAsync(PET_ID, Ct));
        await using (var db = await _db.CreateDbContextAsync(Ct))
        {
            (await db.PetRespectOperations.SingleAsync(Ct)).Completed = true;
            await db.SaveChangesAsync(Ct);
        }
        Assert.True(await inventory.DeletePetAsync(PET_ID, Ct));
    }

    // A use (a saddle strapped on) deletes the used item's row and writes the pet it changed at
    // once, in one transaction.
    [Fact]
    public async Task A_use_deletes_the_item_and_writes_the_pet_at_once()
    {
        _db.Insert(
            new FurnitureDefinitionEntity
            {
                Id = 2840,
                SpriteId = 2840,
                Name = "horse_saddle2",
                ProductType = ProductType.Floor,
                FurniCategory = FurnitureCategory.PetSaddle,
                Logic = "pet_saddle",
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
            }
        );
        _db.Insert(
            new FurnitureEntity
            {
                Id = 41,
                PlayerEntityId = OWNER_ID,
                FurnitureDefinitionEntityId = 2840,
                RoomEntityId = ROOM_ID,
                X = 2,
                Y = 2,
            }
        );
        PetSnapshot saddled;
        await using (var db = await _db.CreateDbContextAsync(Ct))
        {
            var pet = await db.Pets.SingleAsync(Ct);
            pet.RoomEntityId = ROOM_ID;
            await db.SaveChangesAsync(Ct);
            saddled = pet.ToSnapshot("owner") with { HasSaddle = true, SaddleDefinitionId = 2840 };
        }
        var persistence = CreatePersistenceGrain().Persistence;

        var saddle = new RoomFloorItemSnapshot
        {
            ObjectId = 41,
            OwnerId = OWNER_ID,
            OwnerName = "owner",
            DefinitionId = 2840,
            SpriteId = 2840,
            X = 2,
            Y = 2,
            Z = 0,
            Rotation = Rotation.North,
            StackHeight = 1,
            StuffData = new LegacyStuffSnapshot { StuffBitmask = 0, Data = "" },
            ExtraData = string.Empty,
            UsagePolicy = FurnitureUsageType.Everybody,
        };

        await persistence.WriteUseAsync(
            (RoomId)ROOM_ID,
            new UsedUpItem(saddle, TestCustody.Cause()),
            [saddled],
            Ct
        );

        await using var after = await _db.CreateDbContextAsync(Ct);
        (await after.Furnitures.AnyAsync(Ct)).Should().BeFalse("the saddle is used up at once");
        var row = await after.Pets.SingleAsync(Ct);
        row.HasSaddle.Should().BeTrue("the horse wears it from the same write");
        row.SaddleDefinitionId.Should().Be(2840);
    }

    private IInventoryGrain CreateInventory()
    {
        var fakes = new Fakes();
        var grain = GrainHarness.Create(
            typeof(InventoryModule).Assembly,
            "Turbo.Inventory.Grains.InventoryGrain",
            fakes,
            _db,
            OWNER_ID
        );
        var state = RoomHarness.GetMember(grain, "_state")!;
        RoomHarness.SetMember(RoomHarness.GetMember(state, "Pets")!, "IsReady", true);
        RoomHarness.SetField(
            grain,
            "_achievementFacts",
            new AchievementFactRecorder(new ListeningAchievementCatalog())
        );
        var module = Activator.CreateInstance(
            typeof(InventoryModule).Assembly.GetType(
                "Turbo.Inventory.Grains.Modules.InventoryPetModule"
            )!,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            [grain, state, _db, fakes.Create<IPetBreedProvider>(), NullLogger.Instance],
            null
        )!;
        RoomHarness.SetField(grain, "PetModule", module);
        return (IInventoryGrain)grain;
    }

    private IRoomPersistenceGrain CreatePersistenceGrain(Fakes fakes)
    {
        var grain = GrainHarness.Create(
            typeof(RoomGrain).Assembly,
            "Turbo.Rooms.Grains.RoomPersistenceGrain",
            fakes,
            _db,
            ROOM_ID
        );
        RoomHarness.SetField(
            grain,
            "_achievementFacts",
            new AchievementFactRecorder(new ListeningAchievementCatalog())
        );
        RoomHarness.SetMember(RoomHarness.GetMember(grain, "_state")!, "RoomId", (RoomId)ROOM_ID);
        return (IRoomPersistenceGrain)grain;
    }

    private static PlayerEntity NewPlayer(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            PlayerStatus = PlayerStatusType.Offline,
        };

    private static PetEntity NewPet() =>
        new()
        {
            Id = PET_ID,
            PlayerEntityId = OWNER_ID,
            RoomEntityId = null,
            Name = "test-pet",
            TypeId = 1,
            PaletteId = 1,
            BreedId = 1,
            Color = "ffffff",
            Nutrition = 95,
            Respect = 0,
        };
}
