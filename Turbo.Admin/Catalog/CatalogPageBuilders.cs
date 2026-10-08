namespace Turbo.Admin.Catalog;

/// <summary>
/// The page builders the catalog editor runs, by the name the panel sends, and the layouts the
/// pages they build want: each layout reads its offers in its own way (the trophies window groups
/// offers named <c>_g</c>, <c>_s</c> and <c>_b</c>, the pet window reads its pet type from the
/// digits that end its one offer's name), so the builder names offers to suit it.
/// </summary>
public static class CatalogPageBuilders
{
    public const string TROPHIES = "trophies";
    public const string PETS = "pets";
    public const string COLOURS = "colours";
    public const string FURNI_LINE = "furniLine";
    public const string PET_CUSTOMIZATION = "petCustomization";
    public const string EFFECTS = "effects";
    public const string SOLD_LIMITED = "soldLimited";
    public const string SPACES = "spaces";
    public const string POSTERS = "posters";
    public const string BADGE_DISPLAYS = "badgeDisplays";
    public const string SONG_DISCS = "songDiscs";

    public const string TROPHIES_LAYOUT = "trophies";

    /// <summary>The layout of each page the pet builder makes, one pet type to a page.</summary>
    public const string PETS_LAYOUT = "pets";
    public const string COLOURS_LAYOUT = "default_3x3_color_grouping";
    public const string FURNI_LINE_LAYOUT = "default_3x3";
    public const string PET_CUSTOMIZATION_LAYOUT = "petcustomization";
    public const string EFFECTS_LAYOUT = "pixeleffects";
    public const string SOLD_LIMITED_LAYOUT = "sold_ltd_items";

    /// <summary>The room papers page, which previews a room once it sells a floor, a wallpaper and a landscape.</summary>
    public const string SPACES_LAYOUT = "spaces_new";
    public const string POSTERS_LAYOUT = "default_3x3";

    /// <summary>The page that asks the buyer for one of their badges before a display can be bought.</summary>
    public const string BADGE_DISPLAYS_LAYOUT = "badge_display";

    /// <summary>The song disk page, which plays a preview of the song a disk offer's extra parameter names.</summary>
    public const string SONG_DISCS_LAYOUT = "soundmachine";

    /// <summary>The layout a builder's page wants; null for a builder there is not.</summary>
    public static string? LayoutOf(string builder) =>
        builder switch
        {
            TROPHIES => TROPHIES_LAYOUT,
            PETS => PETS_LAYOUT,
            COLOURS => COLOURS_LAYOUT,
            FURNI_LINE => FURNI_LINE_LAYOUT,
            PET_CUSTOMIZATION => PET_CUSTOMIZATION_LAYOUT,
            EFFECTS => EFFECTS_LAYOUT,
            SOLD_LIMITED => SOLD_LIMITED_LAYOUT,
            SPACES => SPACES_LAYOUT,
            POSTERS => POSTERS_LAYOUT,
            BADGE_DISPLAYS => BADGE_DISPLAYS_LAYOUT,
            SONG_DISCS => SONG_DISCS_LAYOUT,
            _ => null,
        };
}
