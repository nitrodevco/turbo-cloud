using FluentAssertions;
using Turbo.Contracts.Hotel;
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
        int respects = 0
    );

    void SetBalance(PlayerId player, string currency, int amount);

    RoomId AddRoom(string name, PlayerId owner, bool isPublic = false);

    void Enter(RoomId room, PlayerId player);

    int AddFurniture(string name);

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

    [Fact]
    public async Task A_room_is_read_with_its_owner_and_nothing_reaches_a_room_no_one_is_in()
    {
        await using var world = NewWorld();
        var owner = world.AddPlayer("Owner", online: true);
        var lobby = world.AddRoom("Lobby", owner, isPublic: true);
        var den = world.AddRoom("Den", owner);
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
}

/// <summary>A wired event the scenarios fire.</summary>
public sealed record HotelWiredPing(int Value) : Turbo.Contracts.Wired.IWiredEvent;

/// <summary>A plugin message the scenarios send.</summary>
[Turbo.Primitives.Plugins.Messages.PluginMessage("hoteltest.ping")]
public sealed record HotelPing(int Value) : Turbo.Primitives.Plugins.Messages.IPluginMessage;
