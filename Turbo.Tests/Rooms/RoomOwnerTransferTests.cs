using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Orleans;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Rooms;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A room given to another player (<c>IRoomOwnershipService</c>). Plugins wrote the room row's
/// owner themselves, so a loaded room went on showing and obeying its old owner until it
/// unloaded, and the new owner kept a rights row they no longer needed. Now the row, the rights
/// and the loaded room change together, and both players standing in it are told their level.
/// </summary>
public sealed class RoomOwnerTransferTests : IDisposable
{
    private const int ROOM = 1;
    private static readonly PlayerId Former = 101;
    private static readonly PlayerId Next = 102;

    private readonly SqliteDb _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public RoomOwnerTransferTests()
    {
        _db.Insert(Player(Former.Value, "former"));
        _db.Insert(Player(Next.Value, "next"));
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
                Id = ROOM,
                Name = "given room",
                PlayerEntityId = Former.Value,
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
        _db.Insert(new RoomRightEntity { RoomEntityId = ROOM, PlayerEntityId = Next.Value });
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task A_loaded_room_shows_and_obeys_its_new_owner_at_once()
    {
        var room = await LoadedRoom();

        var outcome = await room.Harness.Room.TransferOwnershipAsync(Next, "next", Ct);

        outcome.Should().Be(RoomOwnerTransferOutcome.Transferred);
        room.Harness.State.RoomSnapshot.OwnerId.Should().Be(Next);
        room.Harness.State.RoomSnapshot.OwnerName.Should().Be("next");
        room.Harness.State.PlayerIdsWithRights.Should().NotContain(Next);
        room.Harness.Fakes.SentToRoom<RoomInfoUpdatedMessageComposer>().Should().NotBeEmpty();

        // The former owner is told they control nothing here now, then the new one that they own it.
        room.Harness.Fakes.Log.Of("OnControllerLevelUpdatedAsync")
            .Select(x => (RoomControllerType)x.Args[1]!)
            .Should()
            .Equal(RoomControllerType.None, RoomControllerType.Owner);

        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.Rooms.SingleAsync(Ct)).PlayerEntityId.Should().Be(Next.Value);
        (await db.RoomRights.ToListAsync(Ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task The_former_owner_keeps_rights_only_when_the_hotel_says_so()
    {
        var room = await LoadedRoom(new RoomConfig { TransferGivesFormerOwnerRights = true });

        await room.Harness.Room.TransferOwnershipAsync(Next, "next", Ct);

        room.Harness.State.PlayerIdsWithRights.Should().Equal(Former);
        room.Harness.Fakes.Log.Of("OnControllerLevelUpdatedAsync")
            .Select(x => (RoomControllerType)x.Args[1]!)
            .Should()
            .Equal(RoomControllerType.Rights, RoomControllerType.Owner);

        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.RoomRights.Select(x => x.PlayerEntityId).ToListAsync(Ct))
            .Should()
            .Equal(Former.Value);
    }

    [Fact]
    public async Task A_group_home_room_keeps_its_owner_unless_the_hotel_allows_it()
    {
        var kept = await LoadedRoom(group: true);

        (await kept.Harness.Room.TransferOwnershipAsync(Next, "next", Ct))
            .Should()
            .Be(RoomOwnerTransferOutcome.GroupHomeRoom);
        kept.Harness.State.RoomSnapshot.OwnerId.Should().Be(Former);

        await using (var db = await _db.CreateDbContextAsync(Ct))
            (await db.Rooms.SingleAsync(Ct)).PlayerEntityId.Should().Be(Former.Value);

        var given = await LoadedRoom(new RoomConfig { TransferGroupHomeRooms = true }, group: true);

        (await given.Harness.Room.TransferOwnershipAsync(Next, "next", Ct))
            .Should()
            .Be(RoomOwnerTransferOutcome.Transferred);
        given.Harness.State.RoomSnapshot.OwnerId.Should().Be(Next);
    }

    [Fact]
    public async Task The_service_checks_the_room_and_the_player_before_the_room_is_asked()
    {
        var grains = new Fakes();
        grains.Handlers["TransferOwnershipAsync"] = _ =>
            Task.FromResult(RoomOwnerTransferOutcome.Transferred);
        var service = new RoomOwnershipService(
            _db,
            grains.Create<IGrainFactory>(),
            NullLogger<RoomOwnershipService>.Instance
        );

        (await service.TransferAsync(new RoomId(99), Next, Ct))
            .Should()
            .Be(RoomOwnerTransferOutcome.NoSuchRoom);
        (await service.TransferAsync(new RoomId(ROOM), 999, Ct))
            .Should()
            .Be(RoomOwnerTransferOutcome.NoSuchPlayer);
        (await service.TransferAsync(new RoomId(ROOM), Former, Ct))
            .Should()
            .Be(RoomOwnerTransferOutcome.AlreadyOwner);
        grains.Log.Of("TransferOwnershipAsync").Should().BeEmpty();

        (await service.TransferAsync(new RoomId(ROOM), Next, Ct))
            .Should()
            .Be(RoomOwnerTransferOutcome.Transferred);
        grains.Log.Of("TransferOwnershipAsync").Single().Args[1].Should().Be("next");
    }

    /// <summary>The room loaded with both players standing in it; the next owner has rights.</summary>
    private static readonly PlayerId[] Both = [Former, Next];

    private async Task<WiredRoom> LoadedRoom(RoomConfig? config = null, bool group = false)
    {
        var room = new WiredRoom(roomConfig: config);
        var registry = new PermissionRegistry([new CorePermissionNodeSource()]);

        room.Harness.Fakes.Handlers["get_Current"] = call =>
            call.Interface == typeof(IPermissionRegistryProvider) ? registry : Fakes.NotHandled;
        room.Harness.Room.UseDatabase(_db);
        room.Harness.State.RoomSnapshot = room.Harness.State.RoomSnapshot with
        {
            OwnerId = Former,
            OwnerName = "former",
        };
        room.Harness.State.PlayerIdsWithRights.Add(Next);

        if (group)
            room.Harness.Fakes.Handlers["GetGuildOfRoomAsync"] = _ =>
                Task.FromResult<GuildSummarySnapshot?>(
                    (GuildSummarySnapshot)
                        RuntimeHelpers.GetUninitializedObject(typeof(GuildSummarySnapshot)) with
                    {
                        RoomId = ROOM,
                        OwnerId = Former,
                    }
                );

        room.Enter(1, 1, 1, playerId: Former.Value)
            .SetPermissions(ResolvedPermissionsSnapshot.EMPTY);
        room.Enter(2, 2, 2, playerId: Next.Value).SetPermissions(ResolvedPermissionsSnapshot.EMPTY);

        // Each shows the level they walked in with, as entry sets it.
        foreach (var player in Both)
            await room.Harness.Module<RoomSecurityModule>().TakeEntryControllerLevelAsync(player);

        return room;
    }

    private static PlayerEntity Player(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            PlayerStatus = PlayerStatusType.Offline,
        };
}
