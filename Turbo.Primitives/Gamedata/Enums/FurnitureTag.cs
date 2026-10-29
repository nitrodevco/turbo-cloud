namespace Turbo.Primitives.Gamedata.Enums;

/// <summary>
/// Something to know about a furniture definition at a glance: where it came from, whether Habbo
/// has more for it, and whether the client can draw it and players can buy it.
/// </summary>
public enum FurnitureTag
{
    /// <summary>Habbo's newest release has no item of its kind and classname: the hotel's own.</summary>
    HotelOwn = 0,

    /// <summary>Habbo's newest release changes fields the hotel has not changed: an import would take them.</summary>
    HabboUpdate = 1,

    /// <summary>It holds values of the hotel's own where Habbo has others; an import keeps them.</summary>
    Edited = 2,

    /// <summary>No bundle with a file has its asset: the client draws a placeholder.</summary>
    NoBundle = 3,

    /// <summary>Its bundle could not be downloaded or converted.</summary>
    BundleFailed = 4,

    /// <summary>Its bundle is from Habbo at an older revision than Habbo's newest release names: a sync takes the newer one.</summary>
    BundleOutdated = 5,

    /// <summary>
    /// No visible offer on a page players can reach sells it, in the normal catalog or the
    /// Builders Club one, alone or in a bundle of items.
    /// </summary>
    NotSold = 6,

    /// <summary>
    /// The server has a logic for it, by its classname or by the logic its asset file names,
    /// but it runs the plain default (or a logic the server does not have).
    /// </summary>
    LogicMismatch = 7,

    /// <summary>A floor vending definition has no valid list of positive hand-item ids.</summary>
    VendingConfigMissing = 8,
}
