using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Net;
using System.Net.Http;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Turbo.Achievements;
using Turbo.Achievements.Configuration;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Achievements.Enums;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Texts;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Achievements;

public sealed class AchievementBadgeAssetsTests : IDisposable
{
    private const string URL = "https://images.example.com/badges/%badgename%.gif";

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly BadgeHost _host = new();
    private readonly ManualTimeProvider _time = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ABadgeTheHostServesExistsAndIsNotAskedForAgain()
    {
        _host.Serve("ACH_Login1");
        var assets = NewAssets();

        await assets.CheckAsync(["ACH_Login1"], Ct);
        await assets.CheckAsync(["ACH_Login1"], Ct);

        assets.Exists("ACH_Login1").Should().BeTrue();
        _host.Requests.Should().Equal("https://images.example.com/badges/ACH_Login1.gif");
    }

    [Fact]
    public async Task AMissingBadgeIsAskedForAgainOnlyAfterTheRetryWindow()
    {
        var assets = NewAssets();

        await assets.CheckAsync(["ACH_Late1"], Ct);
        _host.Serve("ACH_Late1");
        await assets.CheckAsync(["ACH_Late1"], Ct);

        assets.Exists("ACH_Late1").Should().BeFalse();
        _host.Requests.Should().HaveCount(1);

        _time.Advance(TimeSpan.FromMinutes(2));
        await assets.CheckAsync(["ACH_Late1"], Ct);

        assets.Exists("ACH_Late1").Should().BeTrue();
        _host.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task AnUnreachableHostCountsAsMissingInsteadOfFailing()
    {
        _host.Unreachable = true;
        var assets = NewAssets();

        await assets.CheckAsync(["ACH_Login1"], Ct);

        assets.Exists("ACH_Login1").Should().BeFalse();
    }

    [Fact]
    public void ABadgeNeverCheckedCountsAsMissing()
    {
        _host.Serve("ACH_Login1");

        NewAssets().Exists("ACH_Login1").Should().BeFalse();
    }

    [Theory]
    [InlineData("https://images.example.com/badges/%badgename%.gif", true)]
    [InlineData("http://localhost:8080/%badgename%.png", true)]
    [InlineData("https://images.example.com/badges/ACH.gif", false)]
    [InlineData("/badges/%badgename%.gif", false)]
    [InlineData("ftp://images.example.com/%badgename%.gif", false)]
    public void OnlyAnAbsoluteHttpUrlWithTheBadgeNameTokenIsATemplate(string url, bool valid) =>
        AchievementBadgeAssets.IsValidUrlTemplate(url).Should().Be(valid);

    [Fact]
    public async Task AnEnabledAchievementWhoseBadgeTheHostServesIsImported()
    {
        _host.Serve("ACH_Online1");
        var (catalog, _) = New(WithTexts("ACH_Online"));

        await catalog.ImportAsync([Enabled(100500, "online")], true, "tests", "import", "op", Ct);

        catalog.Current.Should().ContainSingle(x => x.Key == "online");
    }

    [Fact]
    public async Task AnEnabledAchievementWhoseBadgeTheHostLacksIsRefusedNamingItsUrl()
    {
        var (catalog, _) = New(WithTexts("ACH_Online"));

        var import = () =>
            catalog.ImportAsync([Enabled(100500, "online")], true, "tests", "import", "op", Ct);

        await import
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*https://images.example.com/badges/ACH_Online1.gif*");
        catalog.Current.Should().BeEmpty();
    }

    [Fact]
    public async Task AnImageFoundWithoutItsTextsIsRefusedForTheTexts()
    {
        _host.Serve("ACH_Online1");
        var (catalog, _) = New(new Dictionary<string, string>());

        var import = () =>
            catalog.ImportAsync([Enabled(100500, "online")], true, "tests", "import", "op", Ct);

        await import
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*localized badge name and description for ACH_Online1*");
    }

    [Fact]
    public async Task TheSyncReportListsTheUrlOfEachMissingBadge()
    {
        var (_, sync) = New(WithTexts("ACH_Online"));

        var report = await sync.SyncAsync(
            [Enabled(100500, "online")],
            true,
            "tests",
            "sync",
            "sync-op",
            Ct
        );

        report
            .MissingBadgeImages.Should()
            .Equal("https://images.example.com/badges/ACH_Online1.gif");
        report.Applied.Should().BeFalse();
    }

    private AchievementBadgeAssets NewAssets() =>
        new(Options.Create(new AchievementConfig { BadgeAssetUrl = URL }), _time, null, _host);

    private (AchievementCatalog Catalog, AchievementSync Sync) New(
        IReadOnlyDictionary<string, string> texts
    )
    {
        _fakes.Handlers[nameof(IHotelTextProvider.TryGetText)] = call =>
        {
            if (texts.TryGetValue((string)call.Args[0]!, out var value))
            {
                call.Args[1] = value;
                return true;
            }
            call.Args[1] = string.Empty;
            return false;
        };
        var options = Options.Create(new AchievementConfig { BadgeAssetUrl = URL });
        var hotelTexts = _fakes.Create<IHotelTextProvider>();
        var assets = new AchievementBadgeAssets(options, _time, null, _host);
        var catalog = new AchievementCatalog(
            _db,
            _fakes.Create<ICurrencyTypeProvider>(),
            options,
            hotelTexts,
            badgeAssets: assets
        );

        return (catalog, new AchievementSync(catalog, hotelTexts, options, assets));
    }

    private static Dictionary<string, string> WithTexts(string badgeBase) =>
        new()
        {
            ["quests.identity.name"] = "Identity",
            ["badge_name_" + badgeBase] = "Badge",
            ["badge_desc_" + badgeBase] = "Earned %limit% times.",
        };

    private static AchievementDefinition Enabled(int id, string key) =>
        new()
        {
            Id = id,
            Key = key,
            Revision = 1,
            Category = "identity",
            Source = AchievementSources.FIGURE,
            Reducer = AchievementReducer.Counter,
            State = AchievementState.Enabled,
            Levels =
            [
                new()
                {
                    Requirement = 1,
                    BadgeCode = $"ACH_{char.ToUpperInvariant(key[0])}{key[1..]}1",
                },
            ],
        };

    /// <summary>Answers 200 for the badges it serves and 404 for the rest, and records each request.</summary>
    private sealed class BadgeHost : HttpMessageHandler
    {
        private readonly ConcurrentDictionary<string, byte> _served = new();
        private readonly ConcurrentQueue<string> _requests = new();

        public bool Unreachable { get; set; }

        public IReadOnlyList<string> Requests => [.. _requests];

        public void Serve(string badgeCode) => _served.TryAdd(badgeCode, 0);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var url = request.RequestUri!.ToString();
            _requests.Enqueue(url);
            if (Unreachable)
                throw new HttpRequestException("No route to host.");
            var badge = Path.GetFileNameWithoutExtension(request.RequestUri.AbsolutePath);

            return Task.FromResult(
                new HttpResponseMessage(
                    _served.ContainsKey(badge) ? HttpStatusCode.OK : HttpStatusCode.NotFound
                )
            );
        }
    }
}
