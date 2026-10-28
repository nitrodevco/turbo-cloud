using FluentAssertions;
using Turbo.Contracts.Hotel;
using Turbo.Main.Hotel;
using Turbo.Testing;
using Turbo.Tests.Catalog;
using Xunit;

namespace Turbo.Tests.Hotel;

/// <summary>
/// What a plugin driving bots (an NPC, an AI bot) reads and orders through the hotel API: the
/// fake keeps the room's rules (no one in the room, no bots; ten lines in five seconds), and the
/// catalog answers a furni's offers only from pages every player sees.
/// </summary>
public sealed class HotelBotsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_room_no_one_is_in_has_no_bots_out_and_takes_no_order()
    {
        var hotel = new FakeHotel();
        var owner = hotel.Players.Add("Owner");
        var room = hotel.Rooms.Add("Cafe", owner);
        var bot = hotel.Bots.Add(room, "Ada", owner, 1, 1);

        (await hotel.Bots.InRoomAsync(room, Ct)).Should().BeEmpty();
        (await hotel.Bots.SayAsync(room, bot.Id, "hi", false, Ct)).Should().BeFalse();

        hotel.Rooms.Enter(room, owner);

        (await hotel.Bots.InRoomAsync(room, Ct)).Should().ContainSingle(x => x.Name == "Ada");
        (await hotel.Bots.SayAsync(room, bot.Id, "hi", false, Ct)).Should().BeTrue();
        hotel.Bots.AssertSaid(bot.Id, "hi");
    }

    [Fact]
    public async Task A_fake_bot_says_ten_lines_in_five_seconds_of_the_fake_clock()
    {
        var hotel = new FakeHotel();
        var owner = hotel.Players.Add("Owner");
        var room = hotel.Rooms.Add("Cafe", owner);
        hotel.Rooms.Enter(room, owner);
        var bot = hotel.Bots.Add(room, "Ada", owner);

        var said = 0;
        for (var i = 0; i < 12; i++)
            if (await hotel.Bots.SayAsync(room, bot.Id, "hi", false, Ct))
                said++;

        said.Should().Be(10);
        hotel.Clock.Advance(TimeSpan.FromSeconds(5));
        (await hotel.Bots.SayAsync(room, bot.Id, "hi", false, Ct)).Should().BeTrue();
    }

    [Fact]
    public async Task The_host_answers_a_furnis_offers_from_pages_every_player_sees_only()
    {
        using var fixture = new CatalogFixture();
        var provider = fixture.NormalProvider();
        await provider.GetSnapshotAsync(Ct);
        var catalog = new HostHotelCatalog(
            fixture.Db,
            provider,
            fixture.Fakes.Create<Turbo.Primitives.Catalog.Editing.ICatalogEditService>(),
            fixture.Fakes.Create<Turbo.Primitives.Catalog.IExpiringPageService>()
        );

        catalog
            .OffersOf(CatalogFixture.CHAIR)
            .Select(x => x.Id)
            .Should()
            .Contain(CatalogFixture.SOLD)
            .And.NotContain(CatalogFixture.ON_HIDDEN_PAGE);
        catalog.OffersOf(CatalogFixture.POSTER).Should().BeEmpty();
    }
}
