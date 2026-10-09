using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Catalog.Configuration;
using Turbo.Catalog.Reception;
using Turbo.Database.Entities.Catalog;
using Turbo.Primitives.Catalog;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// The reception's expiring page widget: asking with <c>GetCatalogPageWithEarliestExpiry</c>, a
/// client reads the page that runs out first among those still to come - its name, the seconds
/// left and its image, as <c>CatalogPageWithEarliestExpiryMessageParser</c> reads them - and an
/// empty name once none does.
/// </summary>
public sealed class ExpiringPageTests : IDisposable
{
    private static readonly DateTime NOW = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    private readonly CatalogFixture _catalog = new();
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(NOW));
    private readonly ExpiringPageService _pages;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ExpiringPageTests() =>
        _pages = new ExpiringPageService(
            _catalog.Db,
            Options.Create(new CatalogConfig()),
            _time,
            NullLogger<ExpiringPageService>.Instance
        );

    public void Dispose() => _catalog.Dispose();

    [Fact]
    public async Task The_page_that_runs_out_first_is_counted_down_to_and_then_the_next()
    {
        await _pages.SaveAsync(FURNITURE, NOW.AddHours(2), "furni.png", Ct);
        await _pages.SaveAsync(CHILD, NOW.AddMinutes(30), "chairs.png", Ct);

        (await RequestAsync()).Should().Be(("chairs", 1800, "chairs.png"));

        _time.Advance(TimeSpan.FromMinutes(31));

        (await RequestAsync()).Should().Be(("furniture", 5340, "furni.png"), "the chairs ran out");

        _time.Advance(TimeSpan.FromHours(2));

        (await RequestAsync()).Should().Be(("", 0, ""), "no page counts down: the widget hides");
    }

    [Fact]
    public async Task A_page_saved_again_moves_its_expiry_and_one_taken_away_is_not_counted()
    {
        await _pages.SaveAsync(FURNITURE, NOW.AddHours(2), "", Ct);
        await _pages.SaveAsync(FURNITURE, NOW.AddHours(1), "", Ct);

        (await _pages.ListAsync(Ct))
            .Should()
            .ContainSingle()
            .Which.ExpiresAt.Should()
            .Be(NOW.AddHours(1));

        (await _pages.DeleteAsync(FURNITURE, Ct)).Should().BeTrue();
        (await RequestAsync()).PageName.Should().BeEmpty();
    }

    [Fact]
    public async Task A_page_without_a_name_cant_be_counted_down_to()
    {
        _catalog.Db.Insert(Page(50, ROOT, "Nameless"));
        await using (var dbCtx = await _catalog.Db.CreateDbContextAsync(Ct))
        {
            dbCtx.CatalogPages.Single(x => x.Id == 50).Name = null;
            await dbCtx.SaveChangesAsync(Ct);
        }

        var save = () => _pages.SaveAsync(50, NOW.AddHours(1), "", Ct);

        await save.Should().ThrowAsync<ArgumentException>();
    }

    private async Task<(string PageName, int Seconds, string Image)> RequestAsync()
    {
        var harness = new PacketHarness();

        harness.Resolver.Overrides[typeof(IExpiringPageService)] = _pages;
        harness.Resolver.Overrides[typeof(TimeProvider)] = _time;

        var replies = await harness.SendAsync(
            PacketHarness.Incoming("GetCatalogPageWithEarliestExpiryMessageEvent"),
            PacketHarness.Payload(_ => { })
        );
        var packet = replies.Single(x =>
            x.Header == PacketHarness.Outgoing("CatalogPageWithEarliestExpiryMessageComposer")
        );
        var read = (packet.PopString(), packet.PopInt(), packet.PopString());

        packet.Remaining.Should().Be(0);

        return read;
    }
}
