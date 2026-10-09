using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Catalog.Configuration;
using Turbo.Catalog.Reception;
using Turbo.Primitives.Hotel;
using Turbo.Primitives.Hotel.Enums;
using Turbo.Primitives.Hotel.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Catalog;

/// <summary>
/// The reception's promo articles: staff write them, and a client asking with
/// <c>GetPromoArticles</c> reads the visible ones within their dates, in order, as its
/// <c>PromoArticlesDataParser</c> reads them.
/// </summary>
public sealed class PromoArticleTests
{
    private static readonly DateTime NOW = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryDb _db = new();
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(NOW));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task a_client_reads_the_live_articles_in_order_with_every_field()
    {
        var service = Service();

        var first = await service.SaveAsync(
            Article("Summer sale", PromoArticleLinkType.ClientLink, "catalog/open/summer"),
            Ct
        );
        await service.SaveAsync(Article("Hidden", visible: false), Ct);
        await service.SaveAsync(Article("Not yet", starts: NOW.AddDays(1)), Ct);
        await service.SaveAsync(Article("Over", ends: NOW.AddMinutes(-1)), Ct);
        var second = await service.SaveAsync(
            Article("Rare week", PromoArticleLinkType.WebPage, "https://hotel.example/rares"),
            Ct
        );

        await service.ReorderAsync([second.Id, first.Id], Ct);

        var read = await RequestAsync(service);

        read.Select(x => x.Title).Should().Equal("Rare week", "Summer sale");
        read[1]
            .Should()
            .Be(
                new Read(
                    first.Id,
                    "Summer sale",
                    "Body of Summer sale",
                    "Go",
                    1,
                    "catalog/open/summer",
                    "web_promo/summer_sale.png"
                )
            );
    }

    [Fact]
    public async Task an_edit_is_what_the_next_client_reads()
    {
        var service = Service();
        var article = await service.SaveAsync(Article("Old title"), Ct);

        (await RequestAsync(service)).Should().ContainSingle(x => x.Title == "Old title");

        await service.SaveAsync(article with { Title = "New title" }, Ct);
        (await RequestAsync(service)).Should().ContainSingle(x => x.Title == "New title");

        await service.DeleteAsync(article.Id, Ct);
        (await RequestAsync(service)).Should().BeEmpty();
    }

    [Fact]
    public async Task an_article_reaches_players_when_its_start_comes()
    {
        var service = Service();

        await service.SaveAsync(Article("Tomorrow", starts: NOW.AddHours(1)), Ct);
        (await RequestAsync(service)).Should().BeEmpty();

        _time.Advance(TimeSpan.FromHours(1));

        (await RequestAsync(service)).Should().ContainSingle(x => x.Title == "Tomorrow");
    }

    [Theory]
    [InlineData("", null, null)]
    [InlineData("Title", 2, 1)]
    public async Task an_article_it_cant_keep_is_refused(string title, int? startDay, int? endDay)
    {
        var save = () =>
            Service()
                .SaveAsync(
                    Article(title) with
                    {
                        StartsAt = startDay is { } s ? NOW.AddDays(s) : null,
                        EndsAt = endDay is { } e ? NOW.AddDays(e) : null,
                    },
                    Ct
                );

        await save.Should().ThrowAsync<ArgumentException>();
    }

    private PromoArticleService Service() =>
        new(
            _db,
            Options.Create(new CatalogConfig()),
            _time,
            NullLogger<PromoArticleService>.Instance
        );

    private static PromoArticleSnapshot Article(
        string title,
        PromoArticleLinkType link = PromoArticleLinkType.ClientLink,
        string content = "catalog/open/x",
        bool visible = true,
        DateTime? starts = null,
        DateTime? ends = null
    ) =>
        new()
        {
            Id = 0,
            Title = title,
            BodyText = $"Body of {title}",
            ButtonText = "Go",
            LinkType = link,
            LinkContent = content,
            ImageUrl = $"web_promo/{title.ToLowerInvariant().Replace(' ', '_')}.png",
            SortOrder = 0,
            Visible = visible,
            StartsAt = starts,
            EndsAt = ends,
        };

    /// <summary>The articles as the client reads them from the reply.</summary>
    private async Task<List<Read>> RequestAsync(IPromoArticleService service)
    {
        var harness = new PacketHarness();

        harness.Resolver.Overrides[typeof(IPromoArticleService)] = service;

        var replies = await harness.SendAsync(
            PacketHarness.Incoming("GetPromoArticlesMessageEvent"),
            PacketHarness.Payload(_ => { })
        );
        var packet = replies.Single(x =>
            x.Header == PacketHarness.Outgoing("PromoArticlesMessageComposer")
        );
        var count = packet.PopInt();
        var read = new List<Read>();

        for (var i = 0; i < count; i++)
            read.Add(
                new Read(
                    packet.PopInt(),
                    packet.PopString(),
                    packet.PopString(),
                    packet.PopString(),
                    packet.PopInt(),
                    packet.PopString(),
                    packet.PopString()
                )
            );

        packet.Remaining.Should().Be(0);

        return read;
    }

    private sealed record Read(
        int Id,
        string Title,
        string BodyText,
        string ButtonText,
        int LinkType,
        string LinkContent,
        string ImageUrl
    );
}
