using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Database.Entities.Gamedata;
using Turbo.Gamedata;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.History;
using Turbo.Gamedata.Products;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Texts;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Gamedata;

/// <summary>
/// Habbo's product data - the names and descriptions catalog offers show - taken in field by field,
/// with the hotel's own changes kept across Habbo's updates and rolled back as a set.
/// </summary>
public sealed class GamedataProductTests : IDisposable
{
    private static readonly PlayerId STAFF = new(7);

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly GamedataProductService _products;
    private readonly GamedataHistoryService _history;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public GamedataProductTests()
    {
        var config = Options.Create(new GamedataConfig());
        var files = _fakes.Create<IGamedataFileService>();
        var writes = new GamedataWriteLock();

        _products = new GamedataProductService(
            _db,
            config,
            files,
            writes,
            TimeProvider.System,
            NullLogger<GamedataProductService>.Instance
        );
        _history = new GamedataHistoryService(
            _db,
            config,
            _fakes.Create<IFurnitureDefinitionProvider>(),
            files,
            writes,
            _fakes.Create<IHotelTextProvider>(),
            _fakes.Create<Turbo.Primitives.Figures.IFigureDataProvider>(),
            NullLogger<GamedataHistoryService>.Instance
        );
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void the_file_reads_codes_as_text_and_writes_back_by_code()
    {
        var data = Encoding.UTF8.GetBytes(
            """{"productdata":{"product":[{"code":"b","name":"Bee","description":null},{"code":25,"name":"Set","description":"Blue"},{"code":"b","name":"Bee 2","description":"x"}]}}"""
        );

        var products = ProductDataFile.Parse(data);

        products.Keys.Should().Equal("b", "25");
        products["b"].Should().Be(("Bee 2", "x"));
        Encoding
            .UTF8.GetString(
                ProductDataFile.Write(
                    products.Select(x => (x.Key, x.Value.Name, x.Value.Description))
                )
            )
            .Should()
            .Be(
                """{"productdata":{"product":[{"code":"25","name":"Set","description":"Blue"},{"code":"b","name":"Bee 2","description":"x"}]}}"""
            );
    }

    [Fact]
    public void codes_are_trimmed_and_the_unpadded_one_wins()
    {
        var data = Encoding.UTF8.GetBytes(
            "{\"productdata\":{\"product\":[{\"code\":\"avatar_effect27 \",\"name\":\"Padded\"},{\"code\":\"avatar_effect27\",\"name\":\"Viking\"},{\"code\":\"starter \",\"name\":\"Lone\"},{\"code\":\"deal\",\"name\":\"Deal\"},{\"code\":\" deal\",\"name\":\"Padded deal\"}]}}"
        );

        var products = ProductDataFile.Parse(data);

        products.Keys.Should().Equal("avatar_effect27", "starter", "deal");
        products["avatar_effect27"].Name.Should().Be("Viking");
        products["deal"].Name.Should().Be("Deal");
    }

    [Fact]
    public void each_field_is_decided_on_its_own()
    {
        // The hotel renamed it; Habbo changed both: the name stays the hotel's, the description is Habbo's.
        var (action, fields) = GamedataProductService.Decide(
            ("New name", "New desc"),
            ("Mine", "Old desc"),
            ("Old name", "Old desc")
        );

        action.Should().Be(FurnitureImportAction.Update);
        fields.Should().Contain(x => x.Field == "name" && x.Kept);
        fields.Should().Contain(x => x.Field == "description" && !x.Kept);
    }

    [Fact]
    public async Task habbos_products_are_taken_in_and_the_hotels_changes_stay()
    {
        await _products.ImportAsync(
            Version(1, ("chair", "Chair", "Sit"), ("lamp", "Lamp", "Glow")),
            STAFF,
            Ct
        );
        await _products.SaveAsync("chair", "Throne", "Sit", STAFF, Ct);
        await _products.DeleteAsync("lamp", STAFF, Ct);
        await _products.SaveAsync("my_offer", "Ours", "Only here", STAFF, Ct);

        var next = Version(
            2,
            ("chair", "Dining chair", "Sit down"),
            ("lamp", "Lamp", "Glow"),
            ("rug", "Rug", "Soft")
        );
        var preview = await _products.PreviewImportAsync(next, Ct);

        preview!.Added.Should().Be(1);
        preview.Updated.Should().Be(1);

        await _products.ImportAsync(next, STAFF, Ct);

        (await ProductsAsync())
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, (string?, string?)>
                {
                    ["chair"] = ("Throne", "Sit down"),
                    ["rug"] = ("Rug", "Soft"),
                    ["my_offer"] = ("Ours", "Only here"),
                }
            );

        var found = await _products.LookupAsync(["chair", "my_offer", "missing"], Ct);

        found.Should().HaveCount(2);
        found.Single(x => x.Code == "chair").HabboName.Should().Be("Dining chair");
        found.Single(x => x.Code == "my_offer").FromHabbo.Should().BeFalse();
    }

    [Fact]
    public async Task an_import_rolls_back_as_a_whole()
    {
        await _products.ImportAsync(Version(1, ("chair", "Chair", "Sit")), STAFF, Ct);

        var second = await _products.ImportAsync(
            Version(2, ("chair", "Seat", "Sit"), ("rug", "Rug", null)),
            STAFF,
            Ct
        );

        await _history.RollbackAsync(second!.Id, STAFF, Ct);

        (await ProductsAsync())
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, (string?, string?)> { ["chair"] = ("Chair", "Sit") }
            );
    }

    private async Task<Dictionary<string, (string?, string?)>> ProductsAsync()
    {
        await using var dbCtx = _db.CreateDbContext();

        return await dbCtx
            .GamedataProducts.AsNoTracking()
            .ToDictionaryAsync(x => x.Code, x => (x.Name, x.Description), Ct);
    }

    private int Version(int id, params (string Code, string? Name, string? Description)[] products)
    {
        var data = ProductDataFile.Write(products);

        _db.Insert(
            new HabboProductVersionEntity
            {
                Id = id,
                Domain = "com",
                Hash = GamedataBytes.Hash(data),
                Content = GamedataBytes.Compress(data),
                ProductCount = products.Length,
                CheckedAt = DateTime.UtcNow,
            }
        );

        return id;
    }
}
