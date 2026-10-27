using FluentAssertions;
using Turbo.Contracts.Hotel;
using Turbo.Primitives.Availability;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Xunit;

namespace Turbo.Tests.Hotel;

/// <summary>
/// A hotel a contract scenario sets up: players, balances, rooms and furni, then the hotel API over
/// them. <see cref="FakeHotelContractTests"/> builds it in memory (<c>Turbo.Testing.FakeHotel</c>),
/// <see cref="HostHotelContractTests"/> over the host's own implementation, a database and the
/// wallet, inventory and badge grains.
/// </summary>
public interface IHotelWorld : IAsyncDisposable
{
    IHotel Hotel { get; }

    PlayerId AddPlayer(
        string name,
        bool online = false,
        bool systemAccount = false,
        int respects = 0,
        DateTime? lastLogin = null
    );

    void SetBalance(PlayerId player, string currency, int amount);

    RoomId AddRoom(string name, PlayerId owner, bool isPublic = false);

    void Enter(RoomId room, PlayerId player);

    /// <summary>A respect one player gave another, done.</summary>
    void GiveRespect(PlayerId from, PlayerId to);

    /// <summary>The player's saved time online, all of it before any session running now.</summary>
    void SetOnlineTime(PlayerId player, TimeSpan total);

    int AddFurniture(string name);

    /// <summary>The hotel in maintenance: closed to players without the bypass node.</summary>
    void CloseForMaintenance(string reason);

    /// <summary>Staff rename a Public tab category and hide it, as on the panel's Public rooms page.</summary>
    Task StaffRenamePublicCategoryAsync(int id, string name, CancellationToken ct);

    /// <summary>Called once everything is added and before the hotel is used.</summary>
    Task StartAsync(CancellationToken ct);
}

/// <summary>
/// One set of scenarios run against both the fake hotel plugins test with and the host's: what
/// a plugin is told and what it reads back must be the same in both, so a test that passes on the
/// fake means the same on a hotel. A rule changed in one and not the other fails here.
/// </summary>
public abstract class HotelContract
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected abstract IHotelWorld NewWorld();

    [Fact]
    public async Task A_credit_is_made_once_per_reference_and_a_debit_keeps_its_own_receipt()
    {
        await using var world = NewWorld();
        var alice = world.AddPlayer("Alice");
        world.SetBalance(alice, "credits", 5);
        await world.StartAsync(Ct);
        var wallet = world.Hotel.Wallet;

        (await wallet.CreditAsync(alice, "credits", 50, "test:grant-1", "test.grant", Ct))
            .Should()
            .Be(WalletOutcome.Done);
        (await wallet.CreditAsync(alice, "credits", 50, "test:grant-1", "test.grant", Ct))
            .Should()
            .Be(WalletOutcome.AlreadyDone);
        (await wallet.BalanceAsync(alice, "credits", Ct)).Should().Be(55);

        (await wallet.DebitAsync(alice, "credits", 20, "test:grant-1", "test.take_back", Ct))
            .Should()
            .Be(WalletOutcome.Done, "a debit under a credit's reference takes it back");
        (await wallet.DebitAsync(alice, "credits", 20, "test:grant-1", "test.take_back", Ct))
            .Should()
            .Be(WalletOutcome.AlreadyDone);
        (await wallet.BalanceAsync(alice, "credits", Ct)).Should().Be(35);
    }

    [Fact]
    public async Task A_debit_the_balance_cannot_cover_is_refused_and_can_go_through_later()
    {
        await using var world = NewWorld();
        var alice = world.AddPlayer("Alice");
        world.SetBalance(alice, "credits", 10);
        await world.StartAsync(Ct);
        var wallet = world.Hotel.Wallet;

        (await wallet.DebitAsync(alice, "credits", 25, "test:stake-1", "test.stake", Ct))
            .Should()
            .Be(WalletOutcome.Refused);
        (await wallet.BalanceAsync(alice, "credits", Ct)).Should().Be(10);

        await wallet.CreditAsync(alice, "credits", 15, "test:topup-1", "test.topup", Ct);

        (await wallet.DebitAsync(alice, "credits", 25, "test:stake-1", "test.stake", Ct))
            .Should()
            .Be(WalletOutcome.Done, "a refused debit keeps no receipt");
        (await wallet.BalanceAsync(alice, "credits", Ct)).Should().Be(0);
    }

    [Fact]
    public async Task Currencies_are_named_in_any_case_and_a_bad_change_is_refused_or_thrown()
    {
        await using var world = NewWorld();
        var alice = world.AddPlayer("Alice");
        await world.StartAsync(Ct);
        var wallet = world.Hotel.Wallet;

        wallet.CurrencyExists("credits").Should().BeTrue();
        wallet.CurrencyExists("CREDITS").Should().BeTrue();
        wallet.CurrencyExists("beans").Should().BeFalse();
        (await wallet.BalanceAsync(alice, "beans", Ct)).Should().Be(0);
        (await wallet.CreditAsync(alice, "beans", 5, "test:1", "test", Ct))
            .Should()
            .Be(WalletOutcome.Refused);
        (await wallet.CreditAsync(alice, "credits", 0, "test:2", "test", Ct))
            .Should()
            .Be(WalletOutcome.Refused);
        (await wallet.CreditAsync(alice, "Credits", 7, "test:3", "test", Ct))
            .Should()
            .Be(WalletOutcome.Done);
        (await wallet.BalanceAsync(alice, "credits", Ct)).Should().Be(7);

        foreach (var reference in new[] { " ", "#op:1", "#DEBIT:1", new string('x', 129) })
            await FluentActions
                .Awaiting(() => wallet.CreditAsync(alice, "credits", 1, reference, "test", Ct))
                .Should()
                .ThrowAsync<ArgumentException>(reference);
    }

    [Fact]
    public async Task Players_are_found_by_name_in_any_case_and_searched_by_its_start()
    {
        await using var world = NewWorld();
        var alice = world.AddPlayer("Alice");
        var alfred = world.AddPlayer("alfred");
        var bob = world.AddPlayer("Bob");
        await world.StartAsync(Ct);
        var players = world.Hotel.Players;

        (await players.FindAsync("ALICE", Ct))
            .Should()
            .Match<HotelPlayer>(x => x.Id == alice && x.Name == "Alice");
        (await players.FindAsync("nobody", Ct)).Should().BeNull();
        (await players.GetAsync(bob, Ct))!.Name.Should().Be("Bob");
        (await players.GetAsync(new PlayerId(999), Ct)).Should().BeNull();
        (await players.SearchAsync("al", 10, Ct)).Select(x => x.Id).Should().Equal(alfred, alice);
        (await players.SearchAsync("al", 1, Ct)).Select(x => x.Id).Should().Equal(alfred);
        (await players.NamesAsync([alice, bob, new PlayerId(999)], Ct))
            .Should()
            .BeEquivalentTo(new Dictionary<PlayerId, string> { [alice] = "Alice", [bob] = "Bob" });
    }

    [Fact]
    public async Task A_toplist_ranks_highest_first_and_leaves_out_system_accounts()
    {
        await using var world = NewWorld();
        var low = world.AddPlayer("Low", respects: 1);
        var mid = world.AddPlayer("Mid", respects: 5);
        var top = world.AddPlayer("Top", respects: 9);
        world.AddPlayer("Frank", systemAccount: true, respects: 50);
        world.SetBalance(low, "credits", 300);
        world.SetBalance(mid, "credits", 20);
        await world.StartAsync(Ct);
        var players = world.Hotel.Players;

        (await players.TopByAsync(PlayerStat.Respects, 2, 0, Ct))
            .Should()
            .Equal(new HotelRanked(1, top, "Top", 9), new HotelRanked(2, mid, "Mid", 5));
        (await players.TopByAsync(PlayerStat.Respects, 5, 2, Ct))
            .Should()
            .Equal(new HotelRanked(3, low, "Low", 1));
        (await players.TopByAsync(PlayerStat.Balance("credits"), 5, 0, Ct))
            .Select(x => (x.Rank, x.Player, x.Value))
            .Should()
            .Equal((1, low, 300L), (2, mid, 20L));
        (await players.TopByAsync(PlayerStat.Balance("beans"), 5, 0, Ct)).Should().BeEmpty();
    }

    /// <summary>
    /// A plugin seeds its Public tab category as it starts, every time: made once from its
    /// defaults, then left as staff changed it (NAV-STUDY S6: a staff rename survives the plugin's
    /// next start), and changed by the plugin only through its own key.
    /// </summary>
    [Fact]
    public async Task A_plugins_public_category_is_made_once_and_keeps_what_staff_changed()
    {
        await using var world = NewWorld();
        var owner = world.AddPlayer("Owner");
        var lobby = world.AddRoom("Lobby", owner);
        var arcade = world.AddRoom("Arcade", owner);
        var den = world.AddRoom("Den", owner);
        await world.StartAsync(Ct);
        var tab = world.Hotel.Navigator.Public;

        var made = await tab.EnsureCategoryAsync(
            "demo:games",
            new HotelPublicCategoryDefaults("Games", ForceClosed: true, Rooms: [lobby, arcade]),
            Ct
        );

        made.Should()
            .BeEquivalentTo(
                new
                {
                    Key = "demo:games",
                    Name = "Games",
                    Visible = true,
                    ForceClosed = true,
                    Thumbnails = true,
                    StaffPicks = false,
                    Rooms = new[] { lobby, arcade },
                }
            );

        await world.StaffRenamePublicCategoryAsync(made.Id, "Arcades", Ct);
        (await tab.AddRoomAsync("demo:games", den, first: true, Ct)).Should().BeTrue();
        (await tab.AddRoomAsync("demo:games", den, first: true, Ct)).Should().BeFalse();

        var again = await tab.EnsureCategoryAsync(
            "demo:games",
            new HotelPublicCategoryDefaults("Games", Rooms: [lobby]),
            Ct
        );

        (again.Id, again.Name, again.Visible).Should().Be((made.Id, "Arcades", false));
        again.Rooms.Should().Equal(den, lobby, arcade);

        await tab.OrderRoomsAsync("demo:games", [lobby, arcade, den], Ct);
        (await tab.RemoveRoomAsync("demo:games", arcade, Ct)).Should().BeTrue();
        (await tab.RemoveRoomAsync("demo:games", arcade, Ct)).Should().BeFalse();
        (await tab.CategoriesAsync(Ct))
            .Where(x => x.Key is not null)
            .Select(x => (x.Key, string.Join(',', x.Rooms.Select(r => r.Value))))
            .Should()
            .Equal(("demo:games", $"{lobby.Value},{den.Value}"));

        await FluentActions
            .Awaiting(() => tab.AddRoomAsync("demo:other", lobby, first: true, Ct))
            .Should()
            .ThrowAsync<ArgumentException>("the plugin made no category under that key");
        await FluentActions
            .Awaiting(() =>
                tab.EnsureCategoryAsync("games", new HotelPublicCategoryDefaults("Games"), Ct)
            )
            .Should()
            .ThrowAsync<ArgumentException>("a key is <plugin-key>:<name>");
        await FluentActions
            .Awaiting(() => tab.AddRoomAsync("demo:games", new RoomId(999), first: false, Ct))
            .Should()
            .ThrowAsync<ArgumentException>("there is no room 999");
        await FluentActions
            .Awaiting(() => tab.OrderRoomsAsync("demo:games", [lobby], Ct))
            .Should()
            .ThrowAsync<ArgumentException>("an order names every room once");
    }

    [Fact]
    public async Task A_room_is_read_with_its_owner_and_nothing_reaches_a_room_no_one_is_in()
    {
        await using var world = NewWorld();
        var owner = world.AddPlayer("Owner", online: true);
        var lobby = world.AddRoom("Lobby", owner, isPublic: true);
        var den = world.AddRoom("Den", owner);
        var visitor = world.AddPlayer("Visitor", online: true);
        await world.StartAsync(Ct);
        var rooms = world.Hotel.Rooms;

        (await rooms.GetAsync(lobby, Ct))
            .Should()
            .Be(new HotelRoom(lobby, "Lobby", string.Empty, owner, "Owner", IsPublic: true));
        (await rooms.GetAsync(den, Ct))!.IsPublic.Should().BeFalse();
        (await rooms.GetAsync(new RoomId(999), Ct)).Should().BeNull();
        (await rooms.PublicIdsAsync(Ct)).Should().Equal(lobby);
        (await rooms.PlayersAsync(den, Ct)).Should().BeEmpty();
        (await rooms.WhisperAsync(den, owner, "hi", Ct)).Should().BeFalse();
        (await rooms.SendAsync(den, new HotelPing(1), Ct)).Should().BeFalse();

        world.Enter(den, owner);

        (await rooms.PlayersAsync(den, Ct)).Should().Equal(owner);
        (await world.Hotel.Players.CurrentRoomAsync(owner, Ct)).Should().Be(den);
        (await rooms.SendAsync(den, new HotelPing(2), Ct)).Should().BeTrue();
        (await rooms.WhisperAsync(den, owner, "hi", Ct)).Should().BeTrue();

        (await rooms.KickAsync(den, visitor, owner, Ct)).Should().BeFalse("they are not in it");

        world.Enter(den, visitor);

        (await rooms.KickAsync(den, visitor, owner, Ct)).Should().BeTrue();
        (await rooms.PlayersAsync(den, Ct)).Should().Equal(owner);
    }

    [Fact]
    public async Task Furni_is_found_by_name_granted_and_counted_in_circulation_without_system_accounts()
    {
        await using var world = NewWorld();
        var alice = world.AddPlayer("Alice");
        var frank = world.AddPlayer("Frank", systemAccount: true);
        var throne = world.AddFurniture("throne");
        await world.StartAsync(Ct);
        var inventory = world.Hotel.Inventory;

        inventory.FindFurniture("THRONE").Should().Be(new HotelFurniture(throne, "throne", false));
        inventory.Furniture(throne)!.Name.Should().Be("throne");
        inventory.FindFurniture("nothing").Should().BeNull();
        (await inventory.GrantFurnitureAsync(alice, 999, "test.grant", Ct)).Should().BeFalse();

        (await inventory.GrantFurnitureAsync(alice, throne, "test.grant", Ct)).Should().BeTrue();
        (await inventory.GrantFurnitureAsync(alice, throne, "test.grant", Ct)).Should().BeTrue();
        (await inventory.GrantFurnitureAsync(frank, throne, "test.grant", Ct)).Should().BeTrue();

        (await world.Hotel.Catalog.CirculationAsync([throne, 999], Ct))
            .Should()
            .BeEquivalentTo(new Dictionary<int, int> { [throne] = 2 });
    }

    /// <summary>
    /// Pins the 10-25 migration's gap: a prize claimed twice (a retry after a crash) granted its
    /// furni twice, as the grant had no reference.
    /// </summary>
    [Fact]
    public async Task A_furni_grant_under_a_reference_is_made_once_per_player()
    {
        await using var world = NewWorld();
        var alice = world.AddPlayer("Alice");
        var bob = world.AddPlayer("Bob");
        var throne = world.AddFurniture("throne");
        await world.StartAsync(Ct);
        var inventory = world.Hotel.Inventory;

        (await inventory.GrantFurnitureAsync(alice, throne, "test:prize-1", "test.prize", Ct))
            .Should()
            .Be(GrantOutcome.Granted);
        (await inventory.GrantFurnitureAsync(alice, throne, "test:prize-1", "test.prize", Ct))
            .Should()
            .Be(GrantOutcome.AlreadyGranted);
        (await inventory.GrantFurnitureAsync(bob, throne, "test:prize-1", "test.prize", Ct))
            .Should()
            .Be(GrantOutcome.Granted, "a reference is per player");
        (await inventory.GrantFurnitureAsync(alice, 999, "test:prize-2", "test.prize", Ct))
            .Should()
            .Be(GrantOutcome.Refused);

        (await world.Hotel.Catalog.CirculationAsync([throne], Ct))
            .Should()
            .BeEquivalentTo(new Dictionary<int, int> { [throne] = 2 });
    }

    /// <summary>
    /// A hotel's <c>:stats</c>: the respects a player was given and gave, and their time online,
    /// read back as the hotel counts them.
    /// </summary>
    [Fact]
    public async Task A_players_respects_and_time_online_are_read_as_counted()
    {
        await using var world = NewWorld();
        var alice = world.AddPlayer("Alice", respects: 2);
        var bob = world.AddPlayer("Bob");
        world.GiveRespect(bob, alice);
        world.GiveRespect(bob, alice);
        world.GiveRespect(alice, bob);
        world.SetOnlineTime(alice, TimeSpan.FromHours(2));
        await world.StartAsync(Ct);
        var players = world.Hotel.Players;

        (await players.RespectsAsync(alice, Ct)).Should().Be(new HotelRespects(4, 1));
        (await players.RespectsAsync(bob, Ct)).Should().Be(new HotelRespects(1, 2));
        (await players.RespectsAsync(new PlayerId(999), Ct)).Should().BeNull();
        (await players.OnlineTimeAsync(alice, Ct))
            .Should()
            .Be(new HotelOnlineTime(TimeSpan.FromHours(2), TimeSpan.Zero));
        (await players.OnlineTimeAsync(bob, Ct))
            .Should()
            .Be(new HotelOnlineTime(TimeSpan.Zero, TimeSpan.Zero));
        (await players.OnlineTimeAsync(new PlayerId(999), Ct)).Should().BeNull();
    }

    [Fact]
    public async Task A_badge_is_given_once()
    {
        await using var world = NewWorld();
        var alice = world.AddPlayer("Alice");
        await world.StartAsync(Ct);
        var inventory = world.Hotel.Inventory;

        (await inventory.HasBadgeAsync(alice, "ACH_Test1", Ct)).Should().BeFalse();
        (await inventory.GiveBadgeAsync(alice, "ACH_Test1", Ct)).Should().BeTrue();
        (await inventory.GiveBadgeAsync(alice, "ACH_Test1", Ct)).Should().BeFalse();
        (await inventory.HasBadgeAsync(alice, "ACH_Test1", Ct)).Should().BeTrue();
    }

    [Fact]
    public async Task Sends_and_alerts_reach_only_a_player_who_is_online()
    {
        await using var world = NewWorld();
        var online = world.AddPlayer("Online", online: true);
        var offline = world.AddPlayer("Offline");
        await world.StartAsync(Ct);
        var hotel = world.Hotel;

        hotel.Players.OnlineIds().Should().Equal(online);
        (await hotel.Players.SendAsync(online, new HotelPing(1), Ct)).Should().BeTrue();
        (await hotel.Players.SendAsync(offline, new HotelPing(1), Ct)).Should().BeFalse();
        (await hotel.Notify.AlertAsync(online, "test.hello", "Hello %0%", ["you"], Ct))
            .Should()
            .BeTrue();
        (await hotel.Notify.AlertAsync(offline, "test.hello", "Hello %0%", ["you"], Ct))
            .Should()
            .BeFalse();
    }

    [Fact]
    public async Task A_wired_event_reaches_only_a_room_someone_is_in()
    {
        await using var world = NewWorld();
        var owner = world.AddPlayer("Owner", online: true);
        var den = world.AddRoom("Den", owner);
        await world.StartAsync(Ct);
        var wired = world.Hotel.Wired;

        (await wired.FireAsync(den, new HotelWiredPing(1), owner, Ct)).Should().BeFalse();
        (await wired.FireForPlayerAsync(owner, new HotelWiredPing(1), Ct)).Should().BeFalse();

        world.Enter(den, owner);

        (await wired.FireAsync(den, new HotelWiredPing(2), null, Ct)).Should().BeTrue();
        (await wired.FireForPlayerAsync(owner, new HotelWiredPing(3), Ct)).Should().BeTrue();
    }

    /// <summary>
    /// A hotel's front door shows how many were about lately and who, with
    /// their looks. About since a time is logged in then or later, or online now; the hotel's own
    /// accounts are never counted; the last to log in come first.
    /// </summary>
    [Fact]
    public async Task Players_about_lately_are_counted_and_listed_last_login_first()
    {
        var now = new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);
        await using var world = NewWorld();
        var alice = world.AddPlayer("Alice", lastLogin: now.AddDays(-1));
        world.AddPlayer("Bob", lastLogin: now.AddDays(-40));
        var carl = world.AddPlayer("Carl", online: true, lastLogin: now.AddDays(-60));
        world.AddPlayer("Frank", systemAccount: true, lastLogin: now.AddHours(-1));
        var dana = world.AddPlayer("Dana", lastLogin: now.AddHours(-2));
        world.AddPlayer("Erin");
        await world.StartAsync(Ct);
        var players = world.Hotel.Players;

        (await players.CountAsync(now.AddDays(-31), Ct)).Should().Be(3);
        (await players.CountAsync(null, Ct)).Should().Be(5);

        var first = await players.RecentAsync(now.AddDays(-31), 2, 0, Ct);
        var rest = await players.RecentAsync(now.AddDays(-31), 2, 2, Ct);

        first
            .Concat(rest)
            .Select(x => (x.Player.Id, x.Online))
            .Should()
            .Equal((dana, false), (alice, false), (carl, true));
        first[0].Player.Figure.Should().NotBeEmpty();
    }

    /// <summary>
    /// A returning player's old account: a plugin links a Discord account to an imported
    /// player after its own proof. One identity is one player and a player has one of each
    /// service; only staff's replacing link moves one.
    /// </summary>
    [Fact]
    public async Task An_identity_links_to_one_player_and_only_a_replacing_link_moves_it()
    {
        await using var world = NewWorld();
        var alice = world.AddPlayer("Alice");
        var bob = world.AddPlayer("Bob");
        await world.StartAsync(Ct);
        var players = world.Hotel.Players;
        var discord = HotelIdentity.Discord("111", "alice#1");

        (await players.LinkIdentityAsync(alice, discord, false, Ct))
            .Should()
            .Be(IdentityLinkOutcome.Linked);
        (await players.LinkIdentityAsync(alice, discord, false, Ct))
            .Should()
            .Be(IdentityLinkOutcome.AlreadyLinked);
        (await players.LinkIdentityAsync(bob, discord, false, Ct))
            .Should()
            .Be(IdentityLinkOutcome.TakenByAnother);
        (await players.LinkIdentityAsync(alice, HotelIdentity.Discord("222", "alt"), false, Ct))
            .Should()
            .Be(IdentityLinkOutcome.PlayerHasAnother);
        (
            await players.LinkIdentityAsync(
                new PlayerId(99),
                HotelIdentity.Discord("333", "x"),
                false,
                Ct
            )
        )
            .Should()
            .Be(IdentityLinkOutcome.NoSuchPlayer);
        (await players.LinkIdentityAsync(alice, new HotelIdentity("steam", "1", "a"), false, Ct))
            .Should()
            .Be(IdentityLinkOutcome.Unsupported);
        (await players.FindByIdentityAsync(HotelIdentity.DISCORD, "111", Ct)).Should().Be(alice);

        // Staff move it: Bob has it now, Alice has none.
        (await players.LinkIdentityAsync(bob, discord, true, Ct))
            .Should()
            .Be(IdentityLinkOutcome.Linked);
        (await players.FindByIdentityAsync(HotelIdentity.DISCORD, "111", Ct)).Should().Be(bob);
        (await players.UnlinkIdentityAsync(alice, HotelIdentity.DISCORD, Ct)).Should().BeFalse();
        (await players.UnlinkIdentityAsync(bob, HotelIdentity.DISCORD, Ct)).Should().BeTrue();
        (await players.FindByIdentityAsync(HotelIdentity.DISCORD, "111", Ct)).Should().BeNull();
    }

    [Fact]
    public async Task The_hotel_reads_open_until_it_goes_into_maintenance()
    {
        // A plugin could not tell the hotel was closed (a hotel view's night set, fwgaps 10-25).
        await using var world = NewWorld();
        await world.StartAsync(Ct);

        world.Hotel.Status.Current.Phase.Should().Be(HotelAvailabilityPhase.Open);
        world.Hotel.Status.Current.BlocksLogin.Should().BeFalse();

        world.CloseForMaintenance("night");

        world.Hotel.Status.Current.Phase.Should().Be(HotelAvailabilityPhase.Maintenance);
        world.Hotel.Status.Current.BlocksLogin.Should().BeTrue();
        world.Hotel.Status.Current.Reason.Should().Be("night");
    }
}

/// <summary>A wired event the scenarios fire.</summary>
public sealed record HotelWiredPing(int Value) : Turbo.Contracts.Wired.IWiredEvent;

/// <summary>A plugin message the scenarios send.</summary>
[Turbo.Primitives.Plugins.Messages.PluginMessage("hoteltest.ping")]
public sealed record HotelPing(int Value) : Turbo.Primitives.Plugins.Messages.IPluginMessage;
