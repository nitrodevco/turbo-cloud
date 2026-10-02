using System.Collections.Immutable;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Turbo.Achievements;
using Turbo.Database.Achievements;
using Turbo.Database.Entities.Achievements;
using Turbo.Database.Entities.Pets;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Achievements;

public sealed class AchievementAuthoritativeStateTests : IDisposable
{
    private const int PLAYER_ID = 1;
    private readonly SqliteDb _db = new();
    private readonly AchievementFactRecorder _recorder = new(new EmptyCatalog());
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AchievementAuthoritativeStateTests() => _db.Insert(NewPlayer(PLAYER_ID, "state-test"));

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task MembershipStateUnionsEligibleIntervalsAndCountsOnlyPurchasedDaysAsPurchased()
    {
        var now = DateTime.UtcNow;
        _db.Insert(
            new AchievementMembershipIntervalEntity
            {
                Id = 1,
                PlayerId = PLAYER_ID,
                StartUtc = now.AddDays(-10),
                EndUtc = now.AddDays(-7),
                Purchased = true,
            }
        );
        _db.Insert(
            new AchievementMembershipIntervalEntity
            {
                Id = 2,
                PlayerId = PLAYER_ID,
                StartUtc = now.AddDays(-8),
                EndUtc = now.AddDays(-4),
                Purchased = false,
            }
        );
        _db.Insert(
            new AchievementMembershipIntervalEntity
            {
                Id = 3,
                PlayerId = PLAYER_ID,
                StartUtc = now.AddDays(-2),
                EndUtc = now.AddDays(2),
                Purchased = true,
            }
        );

        await new AchievementStateEvaluator(_db, _recorder).RecordAsync(PLAYER_ID, Ct);

        var facts = await ReadFactsAsync();
        ReadAmount(facts, AchievementSources.HC).Should().BeInRange(8 * 86400 - 2, 8 * 86400 + 2);
        ReadAmount(facts, AchievementSources.PURCHASED_HC).Should().Be(7);
    }

    [Fact]
    public async Task OwnedPetStateIncludesInventoryAndRoomPetsOnceAndIgnoresDeletedPets()
    {
        _db.Insert(NewRoomModel());
        _db.Insert(NewRoom(88, PLAYER_ID, score: 0));
        _db.Insert(NewPet(301, PLAYER_ID, roomId: null));
        _db.Insert(NewPet(302, PLAYER_ID, roomId: 88));
        _db.Insert(NewPet(303, PLAYER_ID, roomId: null, deletedAt: DateTime.UtcNow));

        var evaluator = new AchievementStateEvaluator(_db, _recorder);
        await evaluator.RecordAsync(PLAYER_ID, Ct);
        await evaluator.RecordAsync(PLAYER_ID, Ct);

        var facts = await ReadFactsAsync();
        facts.Where(x => x.Source == AchievementSources.PETS).Should().HaveCount(2);
        facts
            .Where(x => x.Source == AchievementSources.PETS)
            .Select(x => x.Amount)
            .Should()
            .OnlyContain(x => x == 2);
    }

    [Fact]
    public async Task RoomRankingExcludesZeroVoteHiddenInvisibleAndDeletedRoomsAndOrdersTiesByDescendingId()
    {
        _db.Insert(NewRoomModel());
        for (var id = 2; id <= 7; id++)
            _db.Insert(NewPlayer(id, $"rank-{id}"));
        _db.Insert(NewRoom(1, 1, score: 0));
        _db.Insert(NewRoom(2, 2, score: 10));
        _db.Insert(NewRoom(3, 3, score: 10));
        _db.Insert(NewRoom(4, 4, score: 20, hidden: true));
        _db.Insert(NewRoom(5, 5, score: 30, doorMode: RoomDoorModeType.Invisible));
        _db.Insert(NewRoom(6, 6, score: 40, deletedAt: DateTime.UtcNow));
        _db.Insert(NewRoom(7, 7, score: 5));

        await using var db = await _db.CreateDbContextAsync(Ct);
        await AchievementRoomCriteria.RecordRankingsAsync(db, _recorder, Ct);
        await db.SaveChangesAsync(Ct);

        var ranks = await ReadFactsAsync();
        ranks
            .Where(x => x.Source == AchievementSources.ROOM_RANK)
            .OrderBy(x => x.Amount)
            .Select(x => (x.PlayerId, x.Amount))
            .Should()
            .Equal((3, 1L), (2, 2L), (7, 3L));
    }

    [Fact]
    public void FloorHeightCriteriaCountsDistinctWalkableBase36Characters()
    {
        AchievementRoomCriteria.CountFloorHeights("xx0012aAbBcC!_-").Should().Be(6);
        AchievementRoomCriteria.CountFloorHeights("xxxxx").Should().Be(0);
    }

    private async Task<List<FactRow>> ReadFactsAsync()
    {
        await using var db = await _db.CreateDbContextAsync(Ct);
        var rows = await db.AchievementFacts.ToListAsync(Ct);
        return rows.Select(row =>
            {
                using var json = JsonDocument.Parse(row.FactJson);
                return new FactRow(
                    row.PlayerId,
                    row.Source,
                    json.RootElement.GetProperty("Amount").GetInt64()
                );
            })
            .ToList();
    }

    private static long ReadAmount(IReadOnlyCollection<FactRow> facts, string source) =>
        facts.Single(x => x.Source == source).Amount;

    private static PlayerEntity NewPlayer(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            PlayerStatus = PlayerStatusType.Offline,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
        };

    private static PetEntity NewPet(int id, int ownerId, int? roomId, DateTime? deletedAt = null) =>
        new()
        {
            Id = id,
            PlayerEntityId = ownerId,
            RoomEntityId = roomId,
            Name = $"pet-{id}",
            TypeId = 1,
            PaletteId = 1,
            BreedId = 1,
            Color = "ffffff",
            DeletedAt = deletedAt,
        };

    private static RoomModelEntity NewRoomModel() =>
        new()
        {
            Id = 1,
            Name = "achievement-test-model",
            Model = "x000aAbBcC",
            DoorX = 0,
            DoorY = 0,
            DoorRotation = Rotation.North,
            Enabled = true,
            Custom = false,
        };

    private static RoomEntity NewRoom(
        int id,
        int ownerId,
        int score,
        bool hidden = false,
        RoomDoorModeType doorMode = RoomDoorModeType.Open,
        DateTime? deletedAt = null
    ) =>
        new()
        {
            Id = id,
            Name = $"room-{id}",
            PlayerEntityId = ownerId,
            DoorMode = doorMode,
            RoomModelEntityId = 1,
            UsersNow = 0,
            PlayersMax = 25,
            WallHeight = -1,
            HideWalls = false,
            ThicknessWall = RoomThicknessType.Normal,
            ThicknessFloor = RoomThicknessType.Normal,
            AllowBlocking = false,
            AllowPets = false,
            AllowPetsEat = false,
            TradeType = RoomTradeModeType.Disabled,
            MuteType = ModSettingType.Owner,
            KickType = ModSettingType.Owner,
            BanType = ModSettingType.Owner,
            ChatFloodType = ChatFloodSensitivityType.Minimal,
            PlayerEntity = null!,
            RoomModelEntity = null!,
            Score = score,
            HiddenByBc = hidden,
            DeletedAt = deletedAt,
        };

    private sealed record FactRow(int PlayerId, string Source, long Amount);

    private sealed class EmptyCatalog : IAchievementCatalog
    {
        public ImmutableArray<AchievementDefinition> Current => [];

        public IDisposable RegisterSources(IEnumerable<AchievementSourceDefinition> sources) =>
            throw new NotSupportedException();

        public Task ReloadAsync(CancellationToken ct) => Task.CompletedTask;

        public Task ImportAsync(
            ImmutableArray<AchievementDefinition> definitions,
            bool apply,
            string actor,
            string reason,
            string operationId,
            CancellationToken ct
        ) => Task.CompletedTask;
    }
}
