namespace Turbo.Primitives.Inventory;

/// <summary>
/// Which avatar effects the hotel lets a player own, by the inventory's settings: an id in range
/// and not one the hotel puts on avatars itself. For code outside the inventory that offers
/// effects, so it never offers one a purchase would refuse.
/// </summary>
public interface IGivableEffects
{
    bool CanGive(int effectId);
}
