using System.Collections.Generic;

namespace Turbo.Primitives.Gamedata;

/// <summary>
/// The gamedata files the hotel builds and serves, by the name Habbo's own addresses give them
/// (<c>/gamedata/furnidata_json/&lt;hash&gt;</c>, <c>/gamedata/external_flash_texts/&lt;hash&gt;</c>),
/// and the name Habbo's <c>/gamedata/hashes</c> lists each under.
/// </summary>
public static class GamedataFiles
{
    public const string FURNITURE_DATA = "furnidata_json";

    /// <summary>The texts the client shows, as Habbo serves them: <c>key=value</c> lines.</summary>
    public const string EXTERNAL_TEXTS = "external_flash_texts";

    /// <summary>The name and description of each product, by the code an offer's name key gives.</summary>
    public const string PRODUCT_DATA = "productdata_json";

    /// <summary>
    /// The colours and clothing avatars are drawn from, as the client loads it (FigureData.json):
    /// Habbo serves the same data as XML at <c>figuredata</c>.
    /// </summary>
    public const string FIGURE_DATA = "figuredata_json";

    public static readonly IReadOnlyList<string> ALL =
    [
        FURNITURE_DATA,
        PRODUCT_DATA,
        EXTERNAL_TEXTS,
        FIGURE_DATA,
    ];

    public static bool IsKnown(string file) =>
        file is FURNITURE_DATA or PRODUCT_DATA or EXTERNAL_TEXTS or FIGURE_DATA;

    /// <summary>The name Habbo's <c>hashes</c> gives the file.</summary>
    public static string HashesName(string file) =>
        file switch
        {
            FURNITURE_DATA => "furnidata",
            PRODUCT_DATA => "productdata",
            EXTERNAL_TEXTS => "external_texts",
            FIGURE_DATA => "figurepartlist_json",
            _ => file,
        };

    public static string ContentType(string file) =>
        file == EXTERNAL_TEXTS ? "text/plain; charset=utf-8" : "application/json; charset=utf-8";
}
