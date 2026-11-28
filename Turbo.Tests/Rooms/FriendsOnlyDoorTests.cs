using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Snapshots.Settings;
using Turbo.Rooms;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Client build 116's room settings offer a fifth door, "friends only"
/// (<c>roomsettings.access_rights.friends</c>, door mode 7): the room's owner picks it like any
/// other, and then only the owner's friends walk in, beside those every door lets in (the owner,
/// rights holders, staff). The client asks nothing before it tries such a door, so anyone else
/// is told there is no entry.
/// </summary>
public sealed class FriendsOnlyDoorTests
{
    private const int OWNER = 1;
    private const int FRIEND = 20;
    private const int STRANGER = 21;
    private const int STAFF = 22;
    private const int RIGHTS_HOLDER = 23;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_owner_saves_a_friends_only_door_and_it_keeps_no_password()
    {
        using var db = new SqliteDb();
        var harness = await OwnedRoomAsync(RoomDoorModeType.Password, db);

        var result = await harness.Room.SaveRoomSettingsAsync(
            ActionContext.CreateForPlayer(OWNER, new RoomId(1)),
            Settings((int)RoomDoorModeType.FriendsOnly, password: "secret"),
            Ct
        );

        result.Error.Should().Be(RoomSettingsSaveErrorType.None);
        harness.State.RoomSnapshot.DoorMode.Should().Be(RoomDoorModeType.FriendsOnly);
        harness.State.RoomSnapshot.Password.Should().BeEmpty();

        await using var ctx = await harness.Room._dbCtxFactory.CreateDbContextAsync(Ct);
        var row = (await ctx.Rooms.FindAsync([1], Ct))!;

        ((int)row.DoorMode).Should().Be(7, "the client's number for the door");
        row.Password.Should().BeNull();
    }

    /// <summary>The client's 5 and 6 are not doors a room is saved with.</summary>
    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(8)]
    public async Task A_door_mode_the_settings_window_does_not_offer_is_refused(int doorMode)
    {
        using var db = new SqliteDb();
        var harness = await OwnedRoomAsync(RoomDoorModeType.Open, db);

        var result = await harness.Room.SaveRoomSettingsAsync(
            ActionContext.CreateForPlayer(OWNER, new RoomId(1)),
            Settings(doorMode, password: string.Empty),
            Ct
        );

        result.Error.Should().Be(RoomSettingsSaveErrorType.Invalid);
        harness.State.RoomSnapshot.DoorMode.Should().Be(RoomDoorModeType.Open);
    }

    [Theory]
    [InlineData(FRIEND, RoomEntryAccessType.Allowed)]
    [InlineData(STRANGER, RoomEntryAccessType.FriendsOnly)]
    [InlineData(OWNER, RoomEntryAccessType.Allowed)]
    [InlineData(STAFF, RoomEntryAccessType.Allowed)]
    [InlineData(RIGHTS_HOLDER, RoomEntryAccessType.Allowed)]
    public async Task A_friends_only_door_lets_in_the_owners_friends_and_whoever_every_door_lets_in(
        int visitor,
        RoomEntryAccessType expected
    )
    {
        var harness = FriendsOnlyRoom();

        (await harness.Module<RoomEntryModule>().CheckAccessAsync(visitor, null, false))
            .Should()
            .Be(expected);
    }

    [Fact]
    public async Task A_friend_is_asked_of_the_owners_friend_list_and_nobody_elses()
    {
        var harness = FriendsOnlyRoom();

        await harness.Module<RoomEntryModule>().CheckAccessAsync(FRIEND, null, false);

        var asked = harness.Fakes.Log.Of("IsFriendAsync").Should().ContainSingle().Subject;

        asked.Key.Should().Be((long)OWNER);
        asked.Args[0].Should().Be((PlayerId)FRIEND);
    }

    [Fact]
    public async Task A_banned_friend_stays_out_and_a_teleporter_arrival_passes_the_door()
    {
        var harness = FriendsOnlyRoom();
        var entry = harness.Module<RoomEntryModule>();

        harness.State.BannedUntilByPlayerId[FRIEND] = harness
            .Clock.GetUtcNow()
            .UtcDateTime.AddHours(1);

        (await entry.CheckAccessAsync(FRIEND, null, false)).Should().Be(RoomEntryAccessType.Banned);
        (await entry.CheckAccessAsync(STRANGER, null, bypassDoor: true))
            .Should()
            .Be(RoomEntryAccessType.Allowed, "as a doorbell or a password door lets one through");
    }

    [Fact]
    public async Task Someone_who_is_no_friend_is_told_there_is_no_entry_and_the_connection_closed()
    {
        var fakes = new Fakes();

        fakes.Handlers["GetPendingRoomAsync"] = _ =>
            Task.FromResult(new RoomPendingSnapshot { RoomId = -1, State = RoomEntryState.None });

        var service = (IRoomService)
            new RoomService(
                NullLogger<IRoomService>.Instance,
                Options.Create(new RoomConfig()),
                fakes.Create<Orleans.IGrainFactory>(),
                TimeProvider.System
            );

        await service.OpenRoomForPlayerIdAsync(
            ActionContext.CreateForPlayer(STRANGER, -1),
            STRANGER,
            1,
            RoomEntryAccessType.FriendsOnly,
            Ct
        );

        fakes
            .Sent()
            .Select(x => x.GetType())
            .Should()
            .Equal(
                typeof(OpenConnectionMessageComposer),
                typeof(CantConnectMessageComposer),
                typeof(CloseConnectionMessageComposer)
            );
        fakes
            .Sent<CantConnectMessageComposer>()
            .Single()
            .ErrorType.Should()
            .Be(RoomConnectionErrorType.NoEntry);
        fakes.Log.Of("GetEntryViewAsync").Should().BeEmpty("the room is not streamed");
    }

    private static LiveRoomHarness FriendsOnlyRoom()
    {
        var harness = new LiveRoomHarness();

        harness.State.RoomSnapshot = harness.State.RoomSnapshot with
        {
            OwnerId = (PlayerId)OWNER,
            DoorMode = RoomDoorModeType.FriendsOnly,
        };
        harness.State.PlayerIdsWithRights.Add(RIGHTS_HOLDER);
        harness.Fakes.Handlers["IsFriendAsync"] = call =>
            Task.FromResult(Equals(call.Key, (long)OWNER) && (PlayerId)call.Args[0]! == FRIEND);
        harness.Fakes.Handlers["HasAsync"] = call =>
            Task.FromResult(
                Equals(call.Key, (long)STAFF)
                    && (string)call.Args[0]! == PermissionNodes.Room.ENTER_LOCKED
            );

        return harness;
    }

    private static RoomSettingsUpdateSnapshot Settings(int doorMode, string password) =>
        new()
        {
            Name = "Party",
            Description = "",
            DoorMode = doorMode,
            Password = password,
            MaximumVisitors = 25,
            CategoryId = null,
            Tags = [],
            TradeMode = RoomTradeModeType.Disabled,
            AllowPets = true,
            AllowPetsEat = true,
            AllowWalkThrough = false,
            HideWalls = false,
            WallThickness = RoomThicknessType.Normal,
            FloorThickness = RoomThicknessType.Normal,
            WhoCanMute = ModSettingType.Owner,
            WhoCanKick = ModSettingType.Owner,
            WhoCanBan = ModSettingType.Owner,
            ChatProtection = ChatFloodSensitivityType.Minimal,
            LeaveOnDoorTile = false,
            IdleSleepEnabled = false,
            IdleSleepTimeoutSeconds = 0,
            IdleAutokickEnabled = false,
            IdleAutokickTimeoutSeconds = 0,
            MuteAllPets = false,
        };

    private static async Task<LiveRoomHarness> OwnedRoomAsync(
        RoomDoorModeType doorMode,
        SqliteDb sqlite
    )
    {
        var password = doorMode == RoomDoorModeType.Password ? "secret" : string.Empty;

        sqlite.Insert(
            new PlayerEntity
            {
                Id = OWNER,
                Name = "owner",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
        sqlite.Insert(
            new RoomModelEntity
            {
                Id = 1,
                Name = "model",
                Model = "00",
                DoorX = 0,
                DoorY = 0,
                DoorRotation = Rotation.North,
                Enabled = true,
                Custom = false,
            }
        );

        var harness = new LiveRoomHarness(db: sqlite);

        harness.State.RoomSnapshot = harness.State.RoomSnapshot with
        {
            OwnerId = (PlayerId)OWNER,
            Name = "Party",
            Description = string.Empty,
            DoorMode = doorMode,
            Password = password,
            Tags = ImmutableArray<string>.Empty,
            ChatProtection = ChatFloodSensitivityType.Minimal,
            AllowPets = true,
            AllowPetsEat = true,
            PlayersMax = 25,
            ModSettings = ModSettingsSnapshot.OwnerOnly,
        };

        await using var ctx = await harness.Room._dbCtxFactory.CreateDbContextAsync(Ct);

        ctx.Rooms.Add(
            new RoomEntity
            {
                Id = 1,
                LastActive = ManualTimeProvider.FixedStart.UtcDateTime,
                Name = "Party",
                PlayerEntityId = OWNER,
                RoomModelEntityId = 1,
                DoorMode = doorMode,
                Password = password.Length == 0 ? null : password,
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
        await ctx.SaveChangesAsync(Ct);

        return harness;
    }
}
