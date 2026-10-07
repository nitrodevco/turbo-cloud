using System.Text;
using System.Text.Json.Nodes;
using FluentAssertions;
using Turbo.Database.Entities.Furniture;
using Turbo.Gamedata.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Xunit;

namespace Turbo.Tests.Gamedata;

/// <summary>
/// FurnitureData.json is written as Habbo writes it: each item's fields in Habbo's order for its
/// kind, colours left out of an item without, and the offers stamped from the catalog.
/// </summary>
public sealed class FurnitureDataWriterTests
{
    private static readonly string[] FLOOR_KEYS =
    [
        "id",
        "classname",
        "revision",
        "category",
        "defaultdir",
        "xdim",
        "ydim",
        "partcolors",
        "name",
        "description",
        "adurl",
        "offerid",
        "buyout",
        "rentofferid",
        "rentbuyout",
        "bc",
        "excludeddynamic",
        "bcofferid",
        "customparams",
        "specialtype",
        "canstandon",
        "cansiton",
        "canlayon",
        "canputstuffon",
        "height",
        "furniline",
        "environment",
        "rare",
        "tradeable",
        "recyclable",
    ];

    private static readonly string[] WALL_KEYS =
    [
        "id",
        "classname",
        "revision",
        "category",
        "name",
        "description",
        "adurl",
        "specialtype",
        "furniline",
        "environment",
        "rare",
        "tradeable",
        "recyclable",
        "offerid",
        "buyout",
        "rentofferid",
        "rentbuyout",
        "bc",
        "excludeddynamic",
        "bcofferid",
    ];

    [Fact]
    public void items_have_habbos_fields_in_habbos_order_for_their_kind()
    {
        var floor = Definition(1, "chair", ProductType.Floor);
        var wall = Definition(2, "poster", ProductType.Wall);

        floor.PartColors = ["#ffffff", "#F7EBBC"];

        var file = Parse(FurnitureDataWriter.Write([floor, wall], Stamps(), Stamps()));

        Keys(file["roomitemtypes"]!["furnitype"]![0]!).Should().Equal(FLOOR_KEYS);
        Keys(file["wallitemtypes"]!["furnitype"]![0]!).Should().Equal(WALL_KEYS);
    }

    [Fact]
    public void an_item_without_colours_has_no_partcolors_and_one_without_a_name_shows_its_classname()
    {
        var floor = Definition(1, "chair", ProductType.Floor);

        var item = FurnitureDataWriter.Item(floor, Stamps(), Stamps());

        item.ContainsKey("partcolors").Should().BeFalse();
        item["name"]!.GetValue<string>().Should().Be("chair");
    }

    [Fact]
    public void the_offers_say_whether_each_catalog_sells_it()
    {
        var regular = Definition(1, "regular", ProductType.Floor, id: 10);
        var buildersClub = Definition(2, "builders", ProductType.Floor, id: 11);
        var neither = Definition(3, "neither", ProductType.Floor, id: 12);

        var offers = Stamps((10, 500));
        var buildersClubOffers = Stamps((11, 600));

        var regularItem = FurnitureDataWriter.Item(regular, offers, buildersClubOffers);
        var buildersClubItem = FurnitureDataWriter.Item(buildersClub, offers, buildersClubOffers);
        var neitherItem = FurnitureDataWriter.Item(neither, offers, buildersClubOffers);

        Offers(regularItem).Should().Be((500, true, -1, false));
        Offers(buildersClubItem).Should().Be((-1, false, 600, true));
        Offers(neitherItem).Should().Be((-1, false, -1, false));
        neitherItem["rentofferid"]!.GetValue<int>().Should().Be(-1);
    }

    [Fact]
    public void texts_are_written_as_they_are()
    {
        var floor = Definition(1, "chair", ProductType.Floor);

        floor.PublicName = "Habbo's Café";

        var text = Encoding.UTF8.GetString(FurnitureDataWriter.Write([floor], Stamps(), Stamps()));

        text.Should().Contain("Habbo's Café");
    }

    private static (int, bool, int, bool) Offers(JsonObject item) =>
        (
            item["offerid"]!.GetValue<int>(),
            item["buyout"]!.GetValue<bool>(),
            item["bcofferid"]!.GetValue<int>(),
            item["bc"]!.GetValue<bool>()
        );

    private static Dictionary<int, int> Stamps(params (int Definition, int Offer)[] stamps) =>
        stamps.ToDictionary(x => x.Definition, x => x.Offer);

    private static JsonNode Parse(byte[] content) => JsonNode.Parse(content)!;

    private static List<string> Keys(JsonNode item) => [.. item.AsObject().Select(x => x.Key)];

    private static FurnitureDefinitionEntity Definition(
        int spriteId,
        string name,
        ProductType type,
        int id = 0
    ) =>
        new()
        {
            Id = id,
            SpriteId = spriteId,
            Name = name,
            ProductType = type,
            FurniCategory = FurnitureCategory.Default,
            Logic = "default_floor",
            Width = 1,
            Length = 1,
            StackHeight = 1,
            CanStack = true,
            CanWalk = false,
            CanSit = false,
            CanLay = false,
            CanRecycle = true,
            CanTrade = true,
            CanGroup = true,
            CanSell = true,
        };
}
