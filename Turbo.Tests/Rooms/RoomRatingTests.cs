using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Turbo.Achievements;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Action;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A vote for a room, through the room grain. A vote moves only the room voted for, so it records
/// only its owner's standing; it recorded the standing of every voted-for room in the hotel, a
/// write per room on every vote.
/// </summary>
public sealed class RoomRatingTests : IDisposable
{
    private const int ROOM = 1;
    private const int OWNER = 1;
    private const int VOTER = 9;

    private readonly SqliteDb _db = new();
    private readonly LiveRoomHarness _room = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public RoomRatingTests()
    {
        _db.Insert(
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

        foreach (var id in new[] { OWNER, 2, 3, 4, VOTER })
            _db.Insert(
                new PlayerEntity
                {
                    Id = id,
                    Name = $"player-{id}",
                    Figure = "hd-180-1",
                    Gender = AvatarGenderType.Male,
                }
            );

        _db.Insert(Room(ROOM, OWNER, score: 0));
        _db.Insert(Room(2, 2, score: 10));
        _db.Insert(Room(3, 3, score: 5));
        _db.Insert(Room(4, 4, score: 3));

        RoomHarness.SetField(_room.Room, "_dbCtxFactory", _db);
        RoomHarness.SetField(
            _room.Room,
            "_achievementFacts",
            new AchievementFactRecorder(new ListeningAchievementCatalog())
        );
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task An_up_vote_records_the_standing_of_the_room_voted_for_and_no_other()
    {
        var voted = await _room.Room.RateRoomAsync(
            ActionContext.CreateForPlayer(VOTER, ROOM),
            1,
            Ct
        );

        voted.Should().BeTrue();
        (await RankFacts()).Should().Equal((OWNER, 4L));
    }

    [Fact]
    public async Task A_down_vote_can_only_lower_a_standing_and_records_none()
    {
        await _room.Room.RateRoomAsync(ActionContext.CreateForPlayer(VOTER, ROOM), -1, Ct);

        (await RankFacts()).Should().BeEmpty();
    }

    private async Task<List<(int Player, long Rank)>> RankFacts()
    {
        await using var db = await _db.CreateDbContextAsync(Ct);
        var rows = await db
            .AchievementFacts.Where(x => x.Source == AchievementSources.ROOM_RANK)
            .ToListAsync(Ct);

        return
        [
            .. rows.Select(row =>
            {
                using var json = JsonDocument.Parse(row.FactJson);
                return (row.PlayerId, json.RootElement.GetProperty("Amount").GetInt64());
            }),
        ];
    }

    private static RoomEntity Room(int id, int ownerId, int score) =>
        new()
        {
            Id = id,
            Name = $"room-{id}",
            PlayerEntityId = ownerId,
            DoorMode = RoomDoorModeType.Open,
            RoomModelEntityId = 1,
            PlayersMax = 25,
            WallHeight = -1,
            HideWalls = false,
            ThicknessWall = RoomThicknessType.Normal,
            ThicknessFloor = RoomThicknessType.Normal,
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
            Score = score,
        };
}
