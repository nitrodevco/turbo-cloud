using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Turbo.Database.Entities.Furniture;
using Turbo.Primitives.Furniture.Enums;

namespace Turbo.Gamedata.Furniture;

/// <summary>
/// FurnitureData.json from the definitions, as Habbo's own file is laid out: <c>roomitemtypes</c>
/// and <c>wallitemtypes</c>, each item's fields in Habbo's order for its kind (a wall item lists
/// its offers last and has no footprint, colours or seating), and <c>partcolors</c> left out of an
/// item without colours. The offers come from the catalog (<see cref="FurnitureOfferStamps"/>):
/// <c>buyout</c> and <c>bc</c> say whether there is one, as they do in Habbo's file, and nothing
/// is rented (<c>rentofferid</c> -1).
/// </summary>
internal static class FurnitureDataWriter
{
    private const int NO_OFFER = -1;

    // Habbo's texts are full of apostrophes and accents: written as they are, not escaped.
    private static readonly JsonWriterOptions WRITER_OPTIONS = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly string[] FLOOR_LEADING =
    [
        "revision",
        "category",
        "defaultdir",
        "xdim",
        "ydim",
        "partcolors",
        "name",
        "description",
        "adurl",
    ];

    private static readonly string[] FLOOR_TRAILING =
    [
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

    private static readonly string[] WALL_LEADING =
    [
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
    ];

    /// <summary>The file, UTF-8, the definitions in the order given.</summary>
    public static byte[] Write(
        IEnumerable<FurnitureDefinitionEntity> definitions,
        IReadOnlyDictionary<int, int> offers,
        IReadOnlyDictionary<int, int> buildersClubOffers
    )
    {
        var floor = new List<JsonObject>();
        var wall = new List<JsonObject>();

        foreach (var definition in definitions)
        {
            if (definition.ProductType == ProductType.Floor)
                floor.Add(Item(definition, offers, buildersClubOffers));
            else if (definition.ProductType == ProductType.Wall)
                wall.Add(Item(definition, offers, buildersClubOffers));
        }

        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(stream, WRITER_OPTIONS))
        {
            writer.WriteStartObject();
            WriteList(writer, "roomitemtypes", floor);
            WriteList(writer, "wallitemtypes", wall);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    /// <summary>One definition's item, as the file writes it.</summary>
    public static JsonObject Item(
        FurnitureDefinitionEntity definition,
        IReadOnlyDictionary<int, int> offers,
        IReadOnlyDictionary<int, int> buildersClubOffers
    )
    {
        var offerId = offers.GetValueOrDefault(definition.Id, NO_OFFER);
        var buildersClubOfferId = buildersClubOffers.GetValueOrDefault(definition.Id, NO_OFFER);
        var item = new JsonObject { ["id"] = definition.SpriteId, ["classname"] = definition.Name };

        if (definition.ProductType == ProductType.Wall)
        {
            AddFields(item, definition, WALL_LEADING);
            AddOffers(item, definition, offerId, buildersClubOfferId);
        }
        else
        {
            AddFields(item, definition, FLOOR_LEADING);
            AddOffers(item, definition, offerId, buildersClubOfferId);
            AddFields(item, definition, FLOOR_TRAILING);
        }

        return item;
    }

    private static void AddFields(
        JsonObject item,
        FurnitureDefinitionEntity definition,
        string[] keys
    )
    {
        foreach (var key in keys)
        {
            var value = FurnitureFields.ByKey[key].Read(definition);

            // Habbo leaves colours out of an item without; a definition without a name of its
            // own (the hotel's furniture before it was given one) shows its classname.
            if (key == "partcolors" && value is null)
                continue;

            if (key == "name" && value is null)
                value = definition.Name;

            item[key] = value;
        }
    }

    private static void AddOffers(
        JsonObject item,
        FurnitureDefinitionEntity definition,
        int offerId,
        int buildersClubOfferId
    )
    {
        item["offerid"] = offerId;
        item["buyout"] = offerId != NO_OFFER;
        item["rentofferid"] = NO_OFFER;
        item["rentbuyout"] = false;
        item["bc"] = buildersClubOfferId != NO_OFFER;
        item["excludeddynamic"] = definition.ExcludedDynamic;
        item["bcofferid"] = buildersClubOfferId;
    }

    private static void WriteList(Utf8JsonWriter writer, string name, List<JsonObject> items)
    {
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        writer.WritePropertyName("furnitype");
        writer.WriteStartArray();

        foreach (var item in items)
            item.WriteTo(writer);

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
