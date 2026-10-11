using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Action;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A room remembers when a player last came in (<c>rooms.last_active_at</c>), which the admin panel
/// lists rooms by. It was never written, so every room looked as active as the day it was made.
/// </summary>
public sealed class RoomLastActiveTests : IDisposable
{
    private const int OWNER = 1;
    private const int ROOM = 1;

    private static readonly DateTime LONG_AGO = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly SqliteDb _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public RoomLastActiveTests()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = OWNER,
                Name = "owner",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
            }
        );
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
        _db.Insert(
            new RoomEntity
            {
                Id = ROOM,
                Name = "room",
                PlayerEntityId = OWNER,
                RoomModelEntityId = 1,
                DoorMode = RoomDoorModeType.Open,
                PlayersMax = 25,
                WallHeight = -1,
                HideWalls = false,
                ThicknessWall = RoomThicknessType.Normal,
                ThicknessFloor = RoomThicknessType.Normal,
                AllowWalkThrough = false,
                AllowPets = true,
                AllowPetsEat = true,
                TradeType = RoomTradeModeType.Disabled,
                MuteType = ModSettingType.Owner,
                KickType = ModSettingType.Owner,
                BanType = ModSettingType.Owner,
                ChatFloodType = ChatFloodSensitivityType.Minimal,
                LastActive = LONG_AGO,
                PlayerEntity = null!,
                RoomModelEntity = null!,
            }
        );
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task A_player_coming_in_marks_the_room_active()
    {
        var room = new LiveRoomHarness();
        RoomHarness.SetField(room.Room, "_dbCtxFactory", _db);
        room.Fakes.Handlers["GetResolvedAsync"] = _ =>
            Task.FromResult(
                Turbo.Primitives.Players.Snapshots.Permissions.ResolvedPermissionsSnapshot.EMPTY
            );
        room.Fakes.Handlers["CreateAvatarFromPlayerSnapshot"] = call => new RoomPlayerAvatar
        {
            ObjectId = (RoomObjectId)call.Args[0]!,
            PlayerId = ((PlayerSummarySnapshot)call.Args[1]!).PlayerId,
        };

        await room.Module<RoomAvatarModule>()
            .CreateAvatarFromPlayerAsync(
                ActionContext.CreateForPlayer((PlayerId)OWNER, (RoomId)ROOM),
                new PlayerSummarySnapshot
                {
                    PlayerId = (PlayerId)OWNER,
                    Name = "owner",
                    Motto = string.Empty,
                    Figure = string.Empty,
                    Gender = default,
                    AchievementScore = 0,
                    BadgesRank = 0,
                    IsOnline = true,
                    CreatedAt = DateTime.UtcNow,
                    LastSeen = DateTime.UtcNow,
                    RespectPoints = 0,
                    RespectsLeft = 0,
                    PetRespectsLeft = 0,
                    RespectReplenishesLeft = 0,
                },
                RoomEntrySnapshot.Default,
                Ct
            );

        await using var db = await _db.CreateDbContextAsync(Ct);

        (await db.Rooms.SingleAsync(Ct)).LastActive.Should().BeAfter(LONG_AGO.AddYears(5));
    }
}
