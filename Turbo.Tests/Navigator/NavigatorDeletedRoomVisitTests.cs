using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Players.Grains.Navigator;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains.Navigator;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Navigator;

/// <summary>
/// A player's room visits are written in batches. A visit to a room deleted before its batch was
/// written could never be stored, and it failed the whole batch: the other visits waited with it,
/// and every retry logged the database error again (the room entry log's key to the room).
/// </summary>
public sealed class NavigatorDeletedRoomVisitTests : IDisposable
{
    private const int PLAYER = 1;
    private const int KEPT_ROOM = 10;
    private const int DELETED_ROOM = 11;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public NavigatorDeletedRoomVisitTests()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = PLAYER,
                Name = "deleted-room-visitor",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
        _db.Insert(
            new RoomModelEntity
            {
                Id = 1,
                Name = "deleted-room-model",
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
                Id = KEPT_ROOM,
                Name = "kept room",
                PlayerEntityId = PLAYER,
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

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task a_visit_to_a_deleted_room_is_dropped_and_the_other_visits_are_written()
    {
        var grain = GrainHarness.Create<PlayerNavigatorGrain>(_fakes, _db, PLAYER);
        await ((Grain)grain).OnActivateAsync(Ct);

        var navigator = (IPlayerNavigatorGrain)grain;
        await navigator.RecordRoomVisitAsync(KEPT_ROOM, Ct);
        await navigator.RecordRoomVisitAsync(DELETED_ROOM, Ct);
        await navigator.RecordRoomVisitAsync(KEPT_ROOM, Ct);

        await ((Grain)grain).OnDeactivateAsync(new(DeactivationReasonCode.None, ""), Ct);

        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.RoomEntryLogs.Select(x => x.RoomEntityId).ToListAsync(Ct))
            .Should()
            .Equal(KEPT_ROOM, KEPT_ROOM);
        GrainHarness.Get<PlayerNavigatorLiveState>(grain).PendingVisits.Count.Should().Be(0);
    }
}
