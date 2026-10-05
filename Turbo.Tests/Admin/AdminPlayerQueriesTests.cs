using FluentAssertions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Admin.Configuration;
using Turbo.Admin.Players;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Moderation;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// Staff finding and looking at players: everyone is searchable, who is online comes from the open
/// sessions, and looking at a player never starts anything for a player who is not online.
/// </summary>
public sealed class AdminPlayerQueriesTests : IDisposable
{
    private const int ALICE = 1;
    private const int BOB = 2;
    private const int CAROL = 3;
    private const int MOD = 4;
    private const int CAFE = 10;

    private static readonly DateTime NOW = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(NOW));
    private PlayerId[] _online = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AdminPlayerQueriesTests()
    {
        _db.Insert(Player(ALICE, "alice", lastLogin: NOW.AddMinutes(-5)));
        _db.Insert(Player(BOB, "bob_100%", lastLogin: NOW.AddDays(-2)));
        _db.Insert(Player(CAROL, "Carol", lastLogin: null));
        _db.Insert(Player(MOD, "moderator", lastLogin: NOW.AddDays(-1)));
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
        _db.Insert(Room(CAFE, "Alice's Café", ALICE));
        _db.Insert(Room(11, "Alice's Attic", ALICE));
        _db.Insert(
            new CurrencyTypeEntity
            {
                Id = 1,
                Name = "Credits",
                CurrencyType = CurrencyType.Credits,
                Enabled = true,
            }
        );
        _db.Insert(
            new CurrencyTypeEntity
            {
                Id = 2,
                Name = null,
                CurrencyType = CurrencyType.Emeralds,
                Enabled = true,
            }
        );
        _db.Insert(
            new PlayerCurrencyEntity
            {
                Id = 1,
                PlayerEntityId = ALICE,
                CurrencyTypeEntityId = 1,
                Amount = 1500,
            }
        );
        _db.Insert(
            new PlayerCurrencyEntity
            {
                Id = 2,
                PlayerEntityId = ALICE,
                CurrencyTypeEntityId = 2,
                Amount = 30,
            }
        );
        _db.Insert(Sanction(1, "spamming", expiresAt: NOW.AddDays(1), revokedAt: null));
        _db.Insert(Sanction(2, "an old one", expiresAt: NOW.AddDays(-1), revokedAt: null));
        _db.Insert(Sanction(3, "a mistake", expiresAt: null, revokedAt: NOW.AddHours(-1)));

        _fakes.Handlers["GetOnlinePlayerIds"] = _ => (IReadOnlyCollection<PlayerId>)_online;
        _fakes.Handlers["GetActiveRoomAsync"] = _ =>
            Task.FromResult(new RoomPointerSnapshot { RoomId = CAFE, ActiveSinceUtc = NOW });
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ANameSearch_IgnoresCase_AndMatchesWildcardsLiterally()
    {
        (await Queries().SearchAsync("CAROL", PlayerSearchMode.Name, false, 1, Ct))
            .Players.Select(x => x.Name)
            .Should()
            .Equal("Carol");

        (await Queries().SearchAsync("100%", PlayerSearchMode.Name, false, 1, Ct))
            .Players.Select(x => x.Name)
            .Should()
            .Equal("bob_100%");

        (await Queries().SearchAsync("al_ce", PlayerSearchMode.Name, false, 1, Ct))
            .Total.Should()
            .Be(0);
    }

    [Fact]
    public async Task AnIdSearch_FindsThatPlayerOnly()
    {
        (await Queries().SearchAsync("2", PlayerSearchMode.Id, false, 1, Ct))
            .Players.Should()
            .ContainSingle()
            .Which.Name.Should()
            .Be("bob_100%");
        (await Queries().SearchAsync("two", PlayerSearchMode.Id, false, 1, Ct))
            .Total.Should()
            .Be(0);
    }

    [Fact]
    public async Task Players_ComeMostRecentlyLoggedInFirst_NeverLoggedInLast()
    {
        var result = await Queries().SearchAsync("", PlayerSearchMode.Name, false, 1, Ct);

        result
            .Players.Select(x => x.Name)
            .Should()
            .Equal("alice", "moderator", "bob_100%", "Carol");
        result.Players[0].RoomsOwned.Should().Be(2);
    }

    [Fact]
    public async Task OnlineOnly_ShowsWhoHasASessionNow()
    {
        _online = [new PlayerId(BOB)];

        var result = await Queries().SearchAsync("", PlayerSearchMode.Name, true, 1, Ct);

        result.OnlineNow.Should().Be(1);
        result.Players.Should().ContainSingle().Which.IsOnline.Should().BeTrue();
    }

    [Fact]
    public async Task APlayersPage_ShowsTheirWalletRoomsAndSanctions()
    {
        var alice = (await Queries().GetAsync(ALICE, Ct))!;

        alice
            .Currencies.Select(x => (x.Name, x.Amount))
            .Should()
            .Equal(("Credits", 1500), ("Emeralds", 30));
        alice.RoomsOwned.Should().Be(2);
        alice.RecentRooms.Select(x => x.Id).Should().BeEquivalentTo([CAFE, 11]);

        var sanctions = alice.Sanctions.ToDictionary(x => x.Reason);

        sanctions["spamming"].IsActive.Should().BeTrue();
        sanctions["spamming"].IssuerName.Should().Be("moderator");
        sanctions["an old one"].IsActive.Should().BeFalse("it has run out");
        sanctions["a mistake"].IsActive.Should().BeFalse("it was lifted");
        sanctions["a mistake"].RevokedByName.Should().Be("moderator");
    }

    [Fact]
    public async Task AnOnlinePlayer_ShowsTheRoomTheyAreIn()
    {
        _online = [new PlayerId(ALICE)];

        var alice = (await Queries().GetAsync(ALICE, Ct))!;

        alice.IsOnline.Should().BeTrue();
        alice.CurrentRoom!.Name.Should().Be("Alice's Café");
    }

    [Fact]
    public async Task LookingAtAnOfflinePlayer_StartsNothingForThem()
    {
        var bob = (await Queries().GetAsync(BOB, Ct))!;

        bob.IsOnline.Should().BeFalse();
        bob.CurrentRoom.Should().BeNull();
        _fakes
            .Log.On<IPlayerPresenceGrain>()
            .Should()
            .BeEmpty("calling a presence grain starts it");
    }

    [Fact]
    public async Task Times_ComeBackMarkedAsUtc_SoTheBrowserDoesNotReadThemAsLocal()
    {
        // A last login is optional (never logged in); it used to come back unmarked, and the panel
        // showed a login minutes ago as hours in the future to anyone west of UTC.
        var players = (
            await Queries().SearchAsync("alice", PlayerSearchMode.Name, false, 1, Ct)
        ).Players;
        var alice = (await Queries().GetAsync(ALICE, Ct))!;

        players[0].LastLoginUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
        alice.LastLoginUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
        alice.LastLoginUtc.Should().Be(NOW.AddMinutes(-5));
        alice
            .Sanctions.First(x => x.Reason == "spamming")
            .ExpiresUtc!.Value.Kind.Should()
            .Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task AnUnknownPlayer_IsNotFound()
    {
        (await Queries().GetAsync(999, Ct)).Should().BeNull();
    }

    private AdminPlayerQueries Queries() =>
        new(
            _db,
            _fakes.Create<IGrainFactory>(),
            _fakes.Create<ISessionGateway>(),
            Options.Create(new AdminConfig()),
            _time
        );

    private static PlayerEntity Player(int id, string name, DateTime? lastLogin) =>
        new()
        {
            Id = id,
            Name = name,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            PlayerStatus = PlayerStatusType.Offline,
            LastLoginAt = lastLogin,
        };

    private static PlayerSanctionEntity Sanction(
        int id,
        string reason,
        DateTime? expiresAt,
        DateTime? revokedAt
    ) =>
        new()
        {
            Id = id,
            PlayerEntityId = ALICE,
            Kind = SanctionKind.Ban,
            Reason = reason,
            IssuerEntityId = MOD,
            ExpiresAt = expiresAt,
            RevokedAt = revokedAt,
            RevokedByEntityId = revokedAt is null ? null : MOD,
        };

    private static RoomEntity Room(int id, string name, int ownerId) =>
        new()
        {
            Id = id,
            Name = name,
            PlayerEntityId = ownerId,
            RoomModelEntityId = 1,
            DoorMode = RoomDoorModeType.Open,
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
            LastActive = NOW.AddDays(-id),
            PlayerEntity = null!,
            RoomModelEntity = null!,
        };
}
