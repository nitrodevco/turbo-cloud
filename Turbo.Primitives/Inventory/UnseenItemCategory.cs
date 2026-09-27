namespace Turbo.Primitives.Inventory;

/// <summary>
/// The inventory tabs the client marks "new" items in (its <c>UnseenItemCategoryEnum</c>). Each
/// category counts ids of its own kind: a furni's item id, a pet's or bot's id, a badge's row id.
/// The client also knows games (6), collectibles (7) and habbicons (8); this server has none.
/// </summary>
public enum UnseenItemCategory
{
    OwnedFurni = 1,
    RentedFurni = 2,
    Pet = 3,
    Badge = 4,
    Bot = 5,
}
