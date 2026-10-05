using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Admin.Configuration;
using Turbo.Admin.Rooms;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Database.Extensions;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// Staff finding and inspecting rooms: every room is searchable (invisible ones too), the page
/// shows what the room holds, and looking never loads a room that is not loaded.
/// </summary>
public sealed class AdminRoomQueriesTests : IDisposable
{
    private const int ALICE = 1;
    private const int BOB = 2;
    private const int CAROL = 3;

    /// <summary>A staff member looking at rooms, holding whatever <see cref="_staffNodes"/> says.</summary>
    private const int STAFF = 50;

    private string[] _staffNodes = [];

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly ManualTimeProvider _time = new(
        new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero)
    );
    private ImmutableArray<RoomActiveSnapshot> _activeRooms = [];
    private ImmutableArray<PlayerId> _insideRoom10 = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AdminRoomQueriesTests()
    {
        _db.Insert(Player(ALICE, "alice"));
        _db.Insert(Player(BOB, "bob"));
        _db.Insert(Player(CAROL, "carol"));
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
        _db.Insert(Room(10, "Alice's Cafe", ALICE, RoomDoorModeType.Open, lastActiveDaysAgo: 1));
        _db.Insert(
            Room(11, "Alice's Secret", ALICE, RoomDoorModeType.Invisible, lastActiveDaysAgo: 2)
        );
        _db.Insert(Room(12, "Bob's Bar", BOB, RoomDoorModeType.Password, lastActiveDaysAgo: 3));

        _fakes.Handlers["GetListingViewAsync"] = _ =>
            Task.FromResult(
                new RoomListingViewSnapshot
                {
                    ActiveRooms = _activeRooms,
                    Epoch = Guid.Empty,
                    Sequence = 0,
                    ChangedKeys = [],
                    IsReset = false,
                }
            );
        _fakes.Handlers["GetActiveRoomIdsAsync"] = _ =>
            Task.FromResult<ImmutableArray<RoomId>>([.. _activeRooms.Select(x => x.RoomId)]);
        _fakes.Handlers["GetRoomPlayersAsync"] = _ => Task.FromResult(_insideRoom10);
        _fakes.Handlers["GetResolvedAsync"] = call =>
            Task.FromResult(
                ResolvedPermissionsSnapshot.EMPTY with
                {
                    Granted = Convert.ToInt64(call.Key) == STAFF ? [.. _staffNodes] : [],
                }
            );
        _fakes.Handlers["GetPlayerNamesAsync"] = call =>
            Task.FromResult(
                ((List<PlayerId>)call.Args[0]!).ToImmutableDictionary(
                    x => x,
                    x => x.Value == BOB ? "bob" : "carol"
                )
            );
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ANameSearchFindsInvisibleRoomsToo()
    {
        var result = await Queries().SearchAsync("alice's", RoomSearchMode.Name, 1, Ct);

        result.Rooms.Select(x => x.Id).Should().Equal(10, 11);
        result.Rooms.Should().Contain(x => x.DoorMode == "Invisible");
    }

    [Fact]
    public async Task SearchWildcardsInTheTextAreMatchedLiterally()
    {
        // An _ or % typed in the search is a character to find, not "anything".
        (await Queries().SearchAsync("alice_s", RoomSearchMode.Name, 1, Ct))
            .Total.Should()
            .Be(0);
        (await Queries().SearchAsync("%", RoomSearchMode.Name, 1, Ct)).Total.Should().Be(0);
    }

    [Fact]
    public async Task AnOwnerSearchMatchesTheStartOfTheirName()
    {
        var result = await Queries().SearchAsync("bo", RoomSearchMode.Owner, 1, Ct);

        result.Rooms.Should().ContainSingle().Which.OwnerName.Should().Be("bob");
    }

    [Theory]
    [InlineData("12", 1)]
    [InlineData("99", 0)]
    [InlineData("twelve", 0)]
    public async Task AnIdSearchFindsThatRoomOnly(string text, int found)
    {
        var result = await Queries().SearchAsync(text, RoomSearchMode.Id, 1, Ct);

        result.Total.Should().Be(found);
    }

    [Fact]
    public async Task ResultsComeAPageAtATimeMostRecentlyActiveFirst()
    {
        var queries = Queries(new AdminConfig { RoomSearchPageSize = 2 });

        var first = await queries.SearchAsync("", RoomSearchMode.Name, 1, Ct);
        var second = await queries.SearchAsync("", RoomSearchMode.Name, 2, Ct);

        first.Total.Should().Be(3);
        first.Rooms.Select(x => x.Id).Should().Equal(10, 11);
        second.Rooms.Select(x => x.Id).Should().Equal(12);
    }

    [Fact]
    public async Task ALoadedRoomShowsItsLivePopulation()
    {
        _activeRooms = [Active(10, population: 4)];

        var result = await Queries().SearchAsync("", RoomSearchMode.Name, 1, Ct);

        var cafe = result.Rooms.Single(x => x.Id == 10);
        cafe.IsLoaded.Should().BeTrue();
        cafe.Population.Should().Be(4);
        result.Rooms.Single(x => x.Id == 12).IsLoaded.Should().BeFalse();
    }

    [Fact]
    public async Task ARoomPageShowsItsSettingsRightsBansAndWhoIsInside()
    {
        _db.Insert(new RoomRightEntity { RoomEntityId = 10, PlayerEntityId = BOB });
        _db.Insert(Ban(1, 10, CAROL, expiresInHours: 1));
        _db.Insert(Ban(2, 10, BOB, expiresInHours: -1));
        _activeRooms = [Active(10, population: 2)];
        _insideRoom10 = [new PlayerId(BOB), new PlayerId(CAROL)];

        var room = await Queries().GetAsync(10, new PlayerId(STAFF), Ct);

        room!.Name.Should().Be("Alice's Cafe");
        room.OwnerName.Should().Be("alice");
        room.Model.Should().Be("model_a");
        room.AllowWalkThrough.Should().BeTrue();
        room.IsLoaded.Should().BeTrue();
        room.IsMuted.Should().BeFalse("a loaded room says whether it is muted");
        room.PlayersInside.Select(x => x.Name).Should().Equal("bob", "carol");
        room.RightsHolders.Should().ContainSingle().Which.Name.Should().Be("bob");
        room.Bans.Should()
            .ContainSingle("an expired ban bans nobody")
            .Which.Name.Should()
            .Be("carol");
    }

    [Fact]
    public async Task APasswordIsNeverShownOnlyThatThereIsOne()
    {
        var room = await Queries().GetAsync(12, new PlayerId(STAFF), Ct);

        room!.HasPassword.Should().BeTrue();
        typeof(Turbo.Admin.Api.Contracts.RoomDetailResponse)
            .GetProperties()
            .Should()
            .NotContain(x => x.Name == "Password");
    }

    [Fact]
    public async Task LookingAtAnUnloadedRoomDoesNotLoadIt()
    {
        var room = await Queries().GetAsync(12, new PlayerId(STAFF), Ct);

        room!.IsLoaded.Should().BeFalse();
        room.IsMuted.Should().BeNull();
        room.PlayersInside.Should().BeEmpty();
        _fakes.Log.On<IRoomGrain>().Should().BeEmpty("calling a room grain loads the room");
    }

    [Fact]
    public async Task AnUnknownRoomIsNotFound()
    {
        (await Queries().GetAsync(999, new PlayerId(STAFF), Ct)).Should().BeNull();
    }

    [Fact]
    public async Task WhatTheViewerMayDo_FollowsTheNodesTheHotelAsksFor()
    {
        _staffNodes = [PermissionNodes.Room.CONTROL_ANY, PermissionNodes.Command.ROOMKICKALL];

        var staff = (await Queries().GetAsync(12, new PlayerId(STAFF), Ct))!.Can;

        staff.EditSettings.Should().BeTrue();
        staff.ManageRights.Should().BeTrue();
        staff.KickAll.Should().BeTrue();
        staff.Moderate.Should().BeFalse("kicks, mutes and bans need room.moderate.any");
        staff.Unload.Should().BeFalse();
        staff.StaffPick.Should().BeFalse();
    }

    [Fact]
    public async Task ARoomsOwner_MayEditAndModerateIt_WithoutAnyNode()
    {
        var owner = (await Queries().GetAsync(12, new PlayerId(BOB), Ct))!.Can;

        owner.EditSettings.Should().BeTrue();
        owner.ManageRights.Should().BeTrue();
        owner.Moderate.Should().BeTrue();
        owner.KickAll.Should().BeFalse("clearing a room is a staff command");
    }

    private AdminRoomQueries Queries(AdminConfig? config = null) =>
        new(
            _db,
            _fakes.Create<IGrainFactory>(),
            Options.Create(config ?? new AdminConfig()),
            _time
        );

    private RoomActiveSnapshot Active(int roomId, int population) =>
        RoomActiveSnapshot.From(
            Room(roomId, "live", ALICE, RoomDoorModeType.Open, 0)
                .ToInfoSnapshot("alice", null, DateTime.UtcNow),
            population
        );

    private RoomBanEntity Ban(int id, int roomId, int playerId, int expiresInHours) =>
        new()
        {
            Id = id,
            RoomEntityId = roomId,
            PlayerEntityId = playerId,
            DateExpires = _time.GetUtcNow().UtcDateTime.AddHours(expiresInHours),
            RoomEntity = null!,
            PlayerEntity = null!,
        };

    private static PlayerEntity Player(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            PlayerStatus = PlayerStatusType.Offline,
        };

    private RoomEntity Room(
        int id,
        string name,
        int ownerId,
        RoomDoorModeType doorMode,
        int lastActiveDaysAgo
    ) =>
        new()
        {
            Id = id,
            Name = name,
            PlayerEntityId = ownerId,
            RoomModelEntityId = 1,
            DoorMode = doorMode,
            Password = doorMode == RoomDoorModeType.Password ? "secret" : null,
            UsersNow = 0,
            PlayersMax = 25,
            WallHeight = -1,
            HideWalls = false,
            ThicknessWall = RoomThicknessType.Normal,
            ThicknessFloor = RoomThicknessType.Normal,
            AllowBlocking = true,
            AllowPets = true,
            AllowPetsEat = true,
            TradeType = RoomTradeModeType.Disabled,
            MuteType = ModSettingType.Owner,
            KickType = ModSettingType.Owner,
            BanType = ModSettingType.Owner,
            ChatFloodType = ChatFloodSensitivityType.Minimal,
            LastActive = _time.GetUtcNow().UtcDateTime.AddDays(-lastActiveDaysAgo),
            PlayerEntity = null!,
            RoomModelEntity = null!,
        };
}
