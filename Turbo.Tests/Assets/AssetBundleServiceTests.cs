using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Database.Entities.Assets;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Pets;
using Turbo.Gamedata.Assets;
using Turbo.Gamedata.Configuration;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Tests.Catalog;
using Xunit;

namespace Turbo.Tests.Assets;

/// <summary>
/// The bundles as the panel works with them: listed by kind, name or id and by status, with
/// whether the hotel names each; checked against the furniture, the catalog's effects and the pet
/// breeds; uploaded, opened and deleted.
/// </summary>
public sealed class AssetBundleServiceTests : IDisposable
{
    private readonly CatalogFixture _catalog = new();
    private readonly AssetFolder _folder;
    private readonly AssetBundleService _service;

    // SQLite generates no ids for rows inserted whole.
    private int _lastBundleId;

    public AssetBundleServiceTests()
    {
        // The fixture's definitions are chair, poster and pet5; lamp has colours.
        _catalog.AddDefinition(20, "lamp*3");
        _folder = new AssetFolder(
            _catalog.Db,
            new AssetBundleConfig
            {
                PageSize = 2,
                CheckSampleLimit = 2,
                UploadMaxMegabytes = 1,
            }
        );
        _service = new AssetBundleService(
            _catalog.Db,
            Options.Create(_folder.Config),
            _folder.Store,
            new AssetBundleChecks(_catalog.Db, Options.Create(_folder.Config), _folder.Store),
            TimeProvider.System,
            NullLogger<IAssetBundleService>.Instance
        );

        Bundle(AssetBundleKind.Furniture, "chair");
        Bundle(AssetBundleKind.Furniture, "old_thing");
        Bundle(AssetBundleKind.Furniture, "ghost", file: false);
        Bundle(
            AssetBundleKind.Furniture,
            "broken",
            hash: false,
            error: "Habbo has no file at its address."
        );
        Bundle(AssetBundleKind.Effect, "Dance1", ids: "1,5");
        Bundle(AssetBundleKind.Pet, "dog", ids: "0");
        Bundle(AssetBundleKind.Pet, "cat", ids: "1");
        Bundle(AssetBundleKind.Figure, "hh_human_body");

        // The catalog sells effects 5 and 7; breeds exist of pet types 0 and 3.
        Product(900, "5");
        Product(901, "7");
        Product(902, "not an effect");
        Breed(1, 0);
        Breed(2, 3);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _folder.Dispose();
        _catalog.Dispose();
    }

    [Fact]
    public async Task The_checks_find_what_the_hotel_lacks_and_what_it_keeps_for_nothing()
    {
        var checks = (await _service.GetChecksAsync(Ct)).ToDictionary(x => x.Id);

        checks
            .Keys.Should()
            .Equal(
                "furniture-missing",
                "file-missing",
                "failed",
                "effects-missing",
                "pets-missing",
                "furniture-unused"
            );

        // lamp, pet5 and poster have no bundle; the samples stop at the limit.
        checks["furniture-missing"].Severity.Should().Be(AssetCheckSeverity.Error);
        checks["furniture-missing"].Count.Should().Be(3);
        checks["furniture-missing"].Samples.Should().Equal("lamp", "pet5");
        checks["file-missing"].Samples.Should().Equal("furniture/ghost");
        checks["failed"]
            .Should()
            .Match<AssetCheckSnapshot>(x =>
                x.Count == 1
                && x.Samples.Single() == "furniture/broken: Habbo has no file at its address."
                && x.Status == AssetBundleStatusFilter.Failed
            );
        checks["effects-missing"].Samples.Should().Equal("effect 7");
        checks["pets-missing"].Samples.Should().Equal("pet type 3");
        checks["furniture-unused"]
            .Should()
            .Match<AssetCheckSnapshot>(x =>
                x.Count == 3
                && x.Kind == AssetBundleKind.Furniture
                && x.Status == AssetBundleStatusFilter.Unused
                && x.Severity == AssetCheckSeverity.Warning
            );
        checks["furniture-unused"].Samples.Should().Equal("broken", "ghost");

        var overview = await _service.GetOverviewAsync(Ct);

        overview.Errors.Should().Be(2);
        overview.Warnings.Should().Be(4);
        overview.Kinds.Select(x => x.Kind).Should().Equal(Enum.GetValues<AssetBundleKind>());
        overview.Kinds[0].Should().Match<AssetKindSummary>(x => x.Bundles == 3 && x.Failed == 1);
    }

    [Fact]
    public async Task Posters_and_campaign_versions_count_for_the_furniture_they_stand_in_for()
    {
        // The poster item has no bundle of its own: each poster's stands in for it, as an ad
        // campaign's version is used while the hotel has the furniture it is a version of.
        _catalog.AddDefinition(21, "ads_cheetos");
        Bundle(AssetBundleKind.Furniture, "poster5");
        Bundle(AssetBundleKind.Furniture, "ads_cheetos_camp");

        var checks = (await _service.GetChecksAsync(Ct)).ToDictionary(x => x.Id);

        checks["furniture-missing"].Count.Should().Be(3, "lamp, pet5 and ads_cheetos; not poster");
        checks["furniture-unused"].Count.Should().Be(3, "old_thing, ghost and broken only");

        var unused = await _service.ListAsync(
            AssetBundleKind.Furniture,
            null,
            AssetBundleStatusFilter.Unused,
            0,
            Ct
        );

        unused.Total.Should().Be(3);

        var all = await _service.ListAsync(
            AssetBundleKind.Furniture,
            "poster5",
            AssetBundleStatusFilter.All,
            0,
            Ct
        );

        all.Items.Single().Used.Should().BeTrue();
    }

    [Fact]
    public async Task The_list_filters_by_status_and_says_which_the_hotel_uses()
    {
        var unused = await _service.ListAsync(null, null, AssetBundleStatusFilter.Unused, 0, Ct);

        unused.Total.Should().Be(4);
        unused.PageSize.Should().Be(2);
        unused.Items.Select(x => x.Name).Should().Equal("broken", "ghost");
        unused.Items.Should().OnlyContain(x => !x.Used);
        (await _service.ListAsync(null, null, AssetBundleStatusFilter.Unused, 1, Ct))
            .Items.Select(x => x.Name)
            .Should()
            .Equal("old_thing", "cat");

        var ok = await _service.ListAsync(null, null, AssetBundleStatusFilter.Ok, 0, Ct);

        ok.Total.Should().Be(7);
        ok.Items.Select(x => (x.Name, x.Used)).Should().Equal(("chair", true), ("ghost", false));

        var failed = await _service.ListAsync(
            AssetBundleKind.Furniture,
            null,
            AssetBundleStatusFilter.Failed,
            0,
            Ct
        );

        failed.Items.Single().Name.Should().Be("broken");

        var pets = await _service.ListAsync(
            AssetBundleKind.Pet,
            null,
            AssetBundleStatusFilter.All,
            0,
            Ct
        );

        pets.Items.Select(x => (x.Name, x.Used)).Should().Equal(("cat", false), ("dog", true));
        (await _service.ListAsync(null, "5", AssetBundleStatusFilter.All, 0, Ct))
            .Items.Single()
            .Should()
            .Match<AssetBundleSnapshot>(x =>
                x.Name == "Dance1" && x.Ids.SequenceEqual(new[] { 1, 5 })
            );
        (await _service.ListAsync(null, "hh_", AssetBundleStatusFilter.All, 0, Ct))
            .Items.Single()
            .Used.Should()
            .BeTrue();
    }

    [Fact]
    public async Task An_upload_is_converted_kept_opened_and_deleted()
    {
        var uploaded = await _service.UploadAsync(
            AssetBundleKind.Furniture,
            null,
            "lamp.swf",
            NitroConverterTests.Swf(),
            PlayerId.Parse(1),
            Ct
        );

        uploaded
            .Should()
            .Match<AssetBundleSnapshot>(x =>
                x.Name == "lamp"
                && x.Source == AssetBundleSource.Upload
                && x.Revision == null
                && x.Hash != null
                && x.Used
            );

        var detail = await _service.GetAsync(AssetBundleKind.Furniture, "lamp", Ct);

        detail!.Path.Should().Be("bundled/furniture/lamp.nitro");
        detail
            .Files.Select(x => x.Name)
            .Should()
            .BeEquivalentTo("test_box.json", "test_box.png", "test_box_spritesheet.json");

        var path = await _service.GetFilePathAsync(AssetBundleKind.Furniture, "lamp", Ct);

        AssetBundleStore.HashOf(await File.ReadAllBytesAsync(path!, Ct)).Should().Be(uploaded.Hash);
        (await _service.GetChecksAsync(Ct))
            .Single(x => x.Id == "furniture-missing")
            .Samples.Should()
            .NotContain("lamp");

        (await _service.DeleteAsync(AssetBundleKind.Furniture, "lamp", PlayerId.Parse(1), Ct))
            .Should()
            .BeTrue();
        File.Exists(path).Should().BeFalse();
        (await _service.GetAsync(AssetBundleKind.Furniture, "lamp", Ct)).Should().BeNull();
        (await _service.DeleteAsync(AssetBundleKind.Furniture, "lamp", PlayerId.Parse(1), Ct))
            .Should()
            .BeFalse();
    }

    [Fact]
    public async Task An_upload_over_a_Habbo_library_keeps_its_ids_and_a_nitro_is_kept_as_it_is()
    {
        var nitro = Turbo
            .Assets.Conversion.NitroConverter.Convert(NitroConverterTests.Swf())
            .Write();

        var uploaded = await _service.UploadAsync(
            AssetBundleKind.Effect,
            "Dance1",
            "my_dance.nitro",
            nitro,
            PlayerId.Parse(1),
            Ct
        );

        uploaded.Ids.Should().Equal(1, 5);
        uploaded.Source.Should().Be(AssetBundleSource.Upload);
        uploaded.Hash.Should().Be(AssetBundleStore.HashOf(nitro));
    }

    [Theory]
    [InlineData("chair.txt", null, "a .swf, .hab or .nitro")]
    [InlineData("chair.swf", "bad name", "can't be a bundle's name")]
    [InlineData("chair.nitro", null, "can't be taken")]
    [InlineData("chair.swf", null, "can't be taken")]
    public async Task An_upload_it_can_not_take_is_refused_with_why(
        string fileName,
        string? name,
        string why
    )
    {
        var act = () =>
            _service.UploadAsync(
                AssetBundleKind.Furniture,
                name,
                fileName,
                "not a library"u8.ToArray(),
                PlayerId.Parse(1),
                Ct
            );

        (await act.Should().ThrowAsync<ArgumentException>()).Which.Message.Should().Contain(why);
    }

    [Fact]
    public async Task An_upload_larger_than_the_limit_is_refused()
    {
        var act = () =>
            _service.UploadAsync(
                AssetBundleKind.Furniture,
                null,
                "big.swf",
                new byte[(1024 * 1024) + 1],
                PlayerId.Parse(1),
                Ct
            );

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*larger than 1 MB*");
    }

    private void Bundle(
        AssetBundleKind kind,
        string name,
        bool hash = true,
        bool file = true,
        string? ids = null,
        string? error = null
    )
    {
        string? written = null;

        if (hash && file)
            written = _folder
                .Store.WriteAsync(
                    kind,
                    name,
                    Turbo
                        .Assets.Conversion.NitroConverter.Convert(NitroConverterTests.Swf())
                        .Write(),
                    CancellationToken.None
                )
                .GetAwaiter()
                .GetResult();

        _catalog.Db.Insert(
            new AssetBundleEntity
            {
                Id = ++_lastBundleId,
                Kind = kind,
                Name = name,
                Revision = "1",
                Source = AssetBundleSource.Habbo,
                Hash = hash ? written ?? "0000" : null,
                Size = 10,
                Ids = ids,
                Error = error,
                UpdatedAt = DateTime.UtcNow,
            }
        );
    }

    private void Product(int id, string extraParam) =>
        _catalog.Db.Insert(
            new CatalogProductEntity
            {
                Id = id,
                CatalogOfferEntityId = CatalogFixture.SOLD,
                ProductType = ProductType.Effect,
                ExtraParam = extraParam,
                Quantity = 1,
                Offer = null!,
            }
        );

    private void Breed(int id, int type) =>
        _catalog.Db.Insert(
            new PetBreedEntity
            {
                Id = id,
                TypeId = type,
                PaletteId = id,
                BreedId = 0,
            }
        );
}
