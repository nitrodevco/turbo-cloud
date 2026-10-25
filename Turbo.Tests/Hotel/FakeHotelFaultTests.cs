using FluentAssertions;
using Turbo.Contracts.Hotel;
using Turbo.Testing;
using Xunit;

namespace Turbo.Tests.Hotel;

/// <summary>
/// What a plugin's tests set up on <see cref="FakeHotel"/> beyond the contract: a write that
/// fails, so a plugin's retry can be tested (the 10-25 migration's view plugin wrapped the fake in
/// 90 lines to refuse one grant), and ids that start where the test needs (it added 8 players to
/// reach the ids it wanted).
/// </summary>
public sealed class FakeHotelFaultTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_failed_write_changes_nothing_keeps_no_receipt_and_its_retry_goes_through()
    {
        var hotel = new FakeHotel();
        var alice = hotel.Players.Add("Alice");
        var throne = hotel.Inventory.AddFurniture("throne");

        hotel.Wallet.FailNext();
        (await hotel.Wallet.CreditAsync(alice, "credits", 5, "test:1", "test.prize", Ct))
            .Should()
            .Be(WalletOutcome.Refused);
        (await hotel.Wallet.CreditAsync(alice, "credits", 5, "test:1", "test.prize", Ct))
            .Should()
            .Be(WalletOutcome.Done);
        hotel.Wallet.AssertCredited(alice, "credits", 5);

        hotel.Inventory.FailNext(throws: new TimeoutException("the grain timed out"));
        var crash = () =>
            hotel.Inventory.GrantFurnitureAsync(alice, throne.DefinitionId, "test:1", "test", Ct);
        await crash.Should().ThrowAsync<TimeoutException>();
        (await crash()).Should().Be(GrantOutcome.Granted);
        (await crash()).Should().Be(GrantOutcome.AlreadyGranted);
        hotel.Inventory.AssertGranted(alice, throne.DefinitionId);
    }

    [Fact]
    public void Ids_count_up_from_where_the_test_sets_them()
    {
        var hotel = new FakeHotel();
        hotel.Players.NextId = 100;
        hotel.Rooms.NextId = 50;
        hotel.Inventory.NextFurnitureId = 7;

        var alice = hotel.Players.Add("Alice");
        var bob = hotel.Players.Add("Bob");

        alice.Id.Value.Should().Be(100);
        bob.Id.Value.Should().Be(101);
        hotel.Rooms.Add("Den", alice).Id.Value.Should().Be(50);
        hotel.Inventory.AddFurniture("throne").DefinitionId.Should().Be(7);

        hotel.Players.NextId = 100;
        var again = () => hotel.Players.Add("Carol");
        again.Should().Throw<ArgumentException>("two players never share an id");
    }
}
