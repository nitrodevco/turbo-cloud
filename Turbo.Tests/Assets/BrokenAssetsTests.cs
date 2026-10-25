using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Assets;
using Turbo.Admin.Catalog;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Plugins;
using Turbo.Primitives.Plugins.Events;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Assets;

/// <summary>
/// The broken assets page over a real store, publish targets and catalog pictures: an address a
/// client was refused (as the client errors plugin's notice tells it) is listed with the fix that
/// applies, and only while that plugin is loaded; what the checks found, the pictures pages show
/// that the hotel lacks and the badges players hold with no picture are listed beside it; and an
/// ignored one is marked with its note until it is brought back.
/// </summary>
public sealed class BrokenAssetsTests : IDisposable
{
    private const string ASSETS = "https://assets.example.com";

    private readonly AssetPublishHotel _hotel = new();
    private readonly Fakes _fakes = new();
    private readonly AdminBrokenAssets _broken;
    private ImmutableArray<PluginSnapshot> _plugins = [Plugin(PluginStateType.Loaded)];
    private ImmutableArray<AssetCheckFinding> _findings = [];
    private readonly HashSet<int> _players = [];

    public BrokenAssetsTests()
    {
        _fakes.Handlers[nameof(IPluginAdministration.List)] = _ => _plugins;
        _fakes.Handlers[nameof(IAssetBundleService.GetCheckFindingsAsync)] = _ =>
            Task.FromResult(_findings);

        _broken = new AdminBrokenAssets(
            _hotel.Db,
            _hotel.Store,
            _fakes.Create<IAssetBundleService>(),
            _hotel.Publishing,
            new AdminCatalogImages(
                _hotel.Db,
                _hotel.Store,
                TimeProvider.System,
                NullLogger<AdminCatalogImages>.Instance
            ),
            _fakes.Create<IPluginAdministration>(),
            TimeProvider.System,
            NullLogger<AdminBrokenAssets>.Instance
        );
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _hotel.Dispose();

    [Fact]
    public async Task A_refused_address_a_client_error_tells_of_is_listed_with_the_fix_that_applies()
    {
        await _hotel.PutBundleAsync("chair", [1, 2, 3], Ct);
        await _hotel.Publishing.CreateTargetAsync(
            new AssetPublishTargetEdit
            {
                Name = "Web root",
                Protocol = AssetPublishProtocol.Folder,
                RemotePath = _hotel.TargetFolder,
                PublicUrl = ASSETS + "/",
            },
            Ct
        );

        await HearAsync($"{ASSETS}/bundled/furniture/chair.nitro");
        await HearAsync($"{ASSETS}/images/catalog/summer_header.gif", issue: 12);
        await HearAsync($"{ASSETS}/images/catalog/summer_header.gif", issue: 13);
        await HearAsync("https://elsewhere.example.com/x.png");

        var list = await ListAsync();
        var client = list.Items.Where(x => x.Source == AdminBrokenAssets.CLIENT).ToList();

        client.Should().HaveCount(3);

        var chair = client.Single(x => x.Key.EndsWith("chair.nitro", StringComparison.Ordinal));

        chair.Kind.Should().Be("furniture");
        chair.Name.Should().Be("chair");
        chair.Actions.Should().Equal(AdminBrokenAssets.PUBLISH);

        var header = client.Single(x =>
            x.Key.EndsWith("summer_header.gif", StringComparison.Ordinal)
        );

        header.Kind.Should().Be("catalog-image");
        header.Name.Should().Be("summer_header.gif");
        header.Count.Should().Be(2);
        header.Link.Should().Be("https://panel.example.com/plugin/clienterrors/issue/13");
        header.Actions.Should().Equal(AdminBrokenAssets.TAKE_FROM_HABBO, AdminBrokenAssets.UPLOAD);

        client
            .Single(x => x.Key.StartsWith("https://elsewhere", StringComparison.Ordinal))
            .Actions.Should()
            .BeEmpty();
        list.Sources.Single(x => x.Id == AdminBrokenAssets.CLIENT).Count.Should().Be(3);

        // Without the plugin loaded, that source is not read and says so.
        _plugins = [Plugin(PluginStateType.Disabled)];

        var without = await ListAsync();
        var source = without.Sources.Single(x => x.Id == AdminBrokenAssets.CLIENT);

        source.Read.Should().BeFalse();
        source.Note.Should().Contain("client errors plugin is not loaded");
        without.Items.Should().NotContain(x => x.Source == AdminBrokenAssets.CLIENT);
    }

    [Fact]
    public void Only_a_client_error_notice_of_a_refused_address_is_heard()
    {
        BrokenAssetNoticeHandler
            .ReportOf(
                Notice("Invalid response 404 for https://a.example.com/b/c.gif\nat x (y.js:1:2)")
            )
            .Should()
            .Be(new BrokenAssetReport("https://a.example.com/b/c.gif", 404, null));
        BrokenAssetNoticeHandler
            .ReportOf(Notice("Invalid response 500 for https://a.example.com/b/c.gif"))
            .Should()
            .BeNull();
        BrokenAssetNoticeHandler.ReportOf(Notice("TypeError: x is undefined")).Should().BeNull();
        BrokenAssetNoticeHandler
            .ReportOf(
                Notice("Invalid response 404 for https://a.example.com/b/c.gif") with
                {
                    Source = "bugreport",
                }
            )
            .Should()
            .BeNull();
    }

    [Fact]
    public async Task What_the_checks_found_and_pictures_and_badges_the_hotel_lacks_are_listed()
    {
        _findings =
        [
            new AssetCheckFinding
            {
                CheckId = "furniture-missing",
                Severity = AssetCheckSeverity.Error,
                Label = "rare_dragon",
                Kind = AssetBundleKind.Furniture,
                Name = "rare_dragon",
            },
            new AssetCheckFinding
            {
                CheckId = "effects-missing",
                Severity = AssetCheckSeverity.Warning,
                Label = "effect 999",
                Kind = AssetBundleKind.Effect,
            },
        ];
        await _hotel.Store.WriteAsync(AssetBundleKind.CatalogImage, "there.gif", [1], Ct);
        await _hotel.Store.WriteAsync(AssetBundleKind.Badge, "ACH_Had1.gif", [1], Ct);

        await using (var dbCtx = await _hotel.Db.CreateDbContextAsync(Ct))
        {
            dbCtx.CatalogPages.Add(Page(1, "Summer", ["summer_header", "there"]));
            dbCtx.CatalogPages.Add(Page(2, "Beach", ["summer_header"]));
            await dbCtx.SaveChangesAsync(Ct);
        }

        Badge(1, 1, "ACH_Had1");
        Badge(2, 1, "ACH_Lost1");
        Badge(3, 2, "ACH_Lost1");

        var list = await ListAsync();

        list.Items.Where(x => x.Source == AdminBrokenAssets.CHECK)
            .Select(x => x.Key)
            .Should()
            .Equal("furniture-missing:furniture/rare_dragon", "effects-missing:effect 999");

        var picture = list.Items.Single(x => x.Source == AdminBrokenAssets.CATALOG);

        picture.Key.Should().Be("summer_header.gif");
        picture.Count.Should().Be(2);
        picture.Detail.Should().Contain("\"Summer\" (1)").And.Contain("\"Beach\" (2)");
        picture.Actions.Should().Equal(AdminBrokenAssets.TAKE_FROM_HABBO, AdminBrokenAssets.UPLOAD);

        var badge = list.Items.Single(x => x.Source == AdminBrokenAssets.BADGE);

        badge.Key.Should().Be("ACH_Lost1");
        badge.Name.Should().Be("ACH_Lost1.gif");
        badge.Count.Should().Be(2);
    }

    [Fact]
    public async Task A_badge_picture_uploaded_leaves_the_list_and_is_published()
    {
        Badge(1, 1, "ACH_Lost1");
        var staff = new PlayerId(7);
        var notPicture = () =>
            _broken.UploadBadgeAsync("ACH_Lost1", "a.txt", "hello"u8.ToArray(), staff, Ct);

        await notPicture.Should().ThrowAsync<ArgumentException>();
        (await ListAsync()).Items.Should().Contain(x => x.Source == AdminBrokenAssets.BADGE);

        await _broken.UploadBadgeAsync(
            "ACH_Lost1",
            "lost.gif",
            "GIF89a...."u8.ToArray(),
            staff,
            Ct
        );

        (await ListAsync()).Items.Should().NotContain(x => x.Source == AdminBrokenAssets.BADGE);
        (await _hotel.Store.ListFilesAsync(Ct))
            .Should()
            .Contain(x => x.Path == "images/badges/ACH_Lost1.gif");
    }

    [Fact]
    public async Task An_ignored_asset_is_marked_with_its_note_until_it_is_brought_back()
    {
        await _hotel.Store.WriteAsync(AssetBundleKind.Badge, "placeholder.gif", [1], Ct);
        Badge(1, 1, "ACH_Lost1");

        var staff = new PlayerId(7);

        (
            await _broken.IgnoreAsync(
                [(AdminBrokenAssets.BADGE, "ACH_Lost1")],
                " Retired ",
                staff,
                Ct
            )
        )
            .Should()
            .BeTrue();
        (await _broken.IgnoreAsync([("nowhere", "ACH_Lost1")], "", staff, Ct)).Should().BeFalse();

        var list = await ListAsync();
        var badge = list.Items.Single(x => x.Source == AdminBrokenAssets.BADGE);

        badge.Ignored!.Note.Should().Be("Retired");
        badge.Ignored.PlayerName.Should().Be("staff7");
        list.Sources.Single(x => x.Id == AdminBrokenAssets.BADGE).Count.Should().Be(0);

        (await _broken.UnignoreAsync([(AdminBrokenAssets.BADGE, "ACH_Lost1")], staff, Ct))
            .Should()
            .BeTrue();

        var back = await ListAsync();

        back.Items.Single(x => x.Source == AdminBrokenAssets.BADGE).Ignored.Should().BeNull();
        back.Sources.Single(x => x.Id == AdminBrokenAssets.BADGE).Count.Should().Be(1);
    }

    [Fact]
    public async Task Several_assets_are_ignored_with_one_note_and_brought_back_together()
    {
        await _hotel.Store.WriteAsync(AssetBundleKind.Badge, "placeholder.gif", [1], Ct);
        Badge(1, 1, "ACH_Lost1");
        Badge(2, 1, "ACH_Lost2");
        Badge(3, 1, "ACH_Lost3");

        var staff = new PlayerId(7);
        (string, string)[] two =
        [
            (AdminBrokenAssets.BADGE, "ACH_Lost1"),
            (AdminBrokenAssets.BADGE, "ACH_Lost2"),
        ];

        // One that can't be kept spoils the lot: none of them is ignored.
        (await _broken.IgnoreAsync([.. two, ("nowhere", "x")], "Retired", staff, Ct))
            .Should()
            .BeFalse();
        (await ListAsync()).Items.Should().OnlyContain(x => x.Ignored == null);

        (await _broken.IgnoreAsync(two, "Retired", staff, Ct)).Should().BeTrue();

        var list = await ListAsync();

        list.Items.Where(x => x.Ignored?.Note == "Retired")
            .Select(x => x.Key)
            .Should()
            .BeEquivalentTo("ACH_Lost1", "ACH_Lost2");
        list.Items.Single(x => x.Key == "ACH_Lost3").Ignored.Should().BeNull();
        list.Sources.Single(x => x.Id == AdminBrokenAssets.BADGE).Count.Should().Be(1);

        (await _broken.UnignoreAsync(two, staff, Ct)).Should().BeTrue();
        (await ListAsync()).Items.Should().OnlyContain(x => x.Ignored == null);
        (await _broken.UnignoreAsync(two, staff, Ct)).Should().BeFalse();
    }

    private Task<BrokenAssetsResponse> ListAsync() =>
        _broken.ListAsync(
            ids =>
                Task.FromResult<IReadOnlyDictionary<int, string>?>(
                    ids.Distinct().ToDictionary(x => x, x => $"staff{x}")
                ),
            Ct
        );

    private Task HearAsync(string url, int issue = 1) =>
        _broken.RecordAsync(
            BrokenAssetNoticeHandler.ReportOf(
                Notice($"Invalid response 404 for {url}") with
                {
                    Url = $"https://panel.example.com/plugin/clienterrors/issue/{issue}",
                }
            )!,
            Ct
        );

    private static PluginNoticeEvent Notice(string text) =>
        new()
        {
            Source = AdminBrokenAssets.CLIENT_ERRORS_PLUGIN,
            Topic = BrokenAssetNoticeHandler.ISSUE_NEW,
            Title = "New client error #1",
            Text = text,
        };

    private void Badge(int id, int playerId, string code)
    {
        if (_players.Add(playerId))
            _hotel.Db.Insert(
                new PlayerEntity
                {
                    Id = playerId,
                    Name = $"player{playerId}",
                    Figure = "hd-180-1",
                    Gender = AvatarGenderType.Male,
                    PlayerStatus = PlayerStatusType.Offline,
                }
            );

        _hotel.Db.Insert(
            new PlayerBadgeEntity
            {
                Id = id,
                PlayerEntityId = playerId,
                BadgeCode = code,
                PlayerEntity = null!,
            }
        );
    }

    private static CatalogPageEntity Page(int id, string title, List<string> images) =>
        new()
        {
            Id = id,
            Localization = title,
            Icon = 1,
            Layout = "default_3x3",
            SortOrder = id,
            Display = CatalogPageDisplay.Regular,
            ImageData = images,
        };

    private static PluginSnapshot Plugin(PluginStateType state) =>
        new(
            AdminBrokenAssets.CLIENT_ERRORS_PLUGIN,
            "Client errors",
            "1.0.0",
            null,
            "clienterrors",
            false,
            state,
            null,
            [],
            [],
            [],
            [],
            [],
            null
        );
}
