using System.Reflection;
using FluentAssertions;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Rooms.Grains;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A room staff pick (the room info's "Add to staff picked rooms", RoomInfoViewCtrl.as:511-515,
/// WIN63-202610201032): the room keeps when it was picked, which orders the staff picks block
/// newest first, every silo's staff picks listing is dropped at once, and the room is told
/// (RoomInfoUpdated) so the caption follows. Unpicked, the time goes with the flag.
/// </summary>
public sealed class RoomStaffPickTests : IDisposable
{
    private readonly SqliteDb _db = new();
    private readonly LiveRoomHarness _room = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public RoomStaffPickTests()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = 2,
                Name = "owner",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
        _db.Insert(
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
        _db.Insert(
            new RoomEntity
            {
                Id = 1,
                Name = "a room",
                PlayerEntityId = 2,
                RoomModelEntityId = 1,
                DoorMode = RoomDoorModeType.Open,
                UsersNow = 0,
                PlayersMax = 25,
                WallHeight = -1,
                HideWalls = false,
                AllowBlocking = false,
                AllowPets = true,
                AllowPetsEat = true,
                ThicknessWall = RoomThicknessType.Normal,
                ThicknessFloor = RoomThicknessType.Normal,
                TradeType = RoomTradeModeType.Disabled,
                MuteType = ModSettingType.Owner,
                KickType = ModSettingType.Owner,
                BanType = ModSettingType.Owner,
                ChatFloodType = ChatFloodSensitivityType.Minimal,
                PlayerEntity = null!,
                RoomModelEntity = null!,
            }
        );
        // The room writes the flag with ExecuteUpdate, which needs a relational database.
        typeof(RoomGrain)
            .GetField("_dbCtxFactory", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(_room.Room, _db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task A_pick_keeps_its_time_drops_the_staff_picks_listing_and_tells_the_room()
    {
        await _room.Room.SetStaffPickAsync(true, Ct);

        (await Row()).Should().Be((true, ManualTimeProvider.FixedStart.UtcDateTime));
        _room
            .Fakes.Log.Of("PublishListingChangesAsync")
            .Select(x => string.Join(',', (IReadOnlyCollection<string>)x.Args[0]!))
            .Should()
            .Equal(NavigatorListingKeys.STAFF_PICKS);
        _room.Fakes.SentToRoom<RoomInfoUpdatedMessageComposer>().Should().ContainSingle();

        await _room.Room.SetStaffPickAsync(false, Ct);

        (await Row()).Should().Be((false, (DateTime?)null));
    }

    private async Task<(bool, DateTime?)> Row()
    {
        await using var db = await _db.CreateDbContextAsync(Ct);
        var row = db.Rooms.Single(x => x.Id == 1);

        return (row.StaffPick, row.StaffPickedAt);
    }
}
