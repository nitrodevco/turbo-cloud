namespace Turbo.Primitives.Movements.Enums;

/// <summary>Where an asset is. Stored by value: never renumber a member.</summary>
public enum PlaceKind : byte
{
    /// <summary>Does not exist: where an asset comes from when created and goes to when destroyed.</summary>
    None = 0,
    Inventory = 1,
    Wallet = 2,
    Room = 3,

    /// <summary>Inside another furni (a present, a wired chest, a jukebox); the place id is the holder's item id.</summary>
    Holder = 4,

    /// <summary>A listed item or revenue the marketplace holds; the place id is the offer id.</summary>
    MarketplaceOffer = 5,

    /// <summary>A wired chest's credit balance; the place id is the chest's item id.</summary>
    ChestCredits = 6,
}
