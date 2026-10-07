using System.Collections.Generic;
using System.Globalization;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Inventory;

namespace Turbo.Primitives.Catalog;

/// <summary>
/// How a catalog product of type effect names its effect. It has no furniture definition, so
/// the effect id is the product's extra parameter, as a badge product's is its badge code; the
/// product's quantity is the number of copies. The client reads the id from the integer after
/// the product type (it draws <c>fx_icon_&lt;id&gt;</c>), which the snapshot fills in.
/// </summary>
public static class EffectProducts
{
    /// <summary>The effect id the extra parameter names, or false when it names none.</summary>
    public static bool TryGetEffectId(string? extraParam, out int effectId) =>
        int.TryParse(extraParam, NumberStyles.None, CultureInfo.InvariantCulture, out effectId)
        && effectId > 0;

    /// <summary>
    /// The copies of each effect that buying the offer <paramref name="quantity"/> times gives,
    /// the copies of one effect across its products added up (the grant adds them the same way).
    /// Null when a product names no effect, which is an offer set up wrongly rather than a
    /// purchase that failed.
    /// </summary>
    public static Dictionary<int, long>? CountCopies(
        IEnumerable<CatalogProductSnapshot> products,
        int quantity
    )
    {
        var copiesByEffect = new Dictionary<int, long>();

        foreach (var product in products)
        {
            if (product.ProductType != ProductType.Effect)
                continue;

            if (!TryGetEffectId(product.ExtraParam, out var effectId))
                return null;

            copiesByEffect[effectId] =
                copiesByEffect.GetValueOrDefault(effectId) + ((long)product.Quantity * quantity);
        }

        return copiesByEffect;
    }

    /// <summary>
    /// The refusal the client words for a purchase that cannot be given: its own "you already own
    /// this effect" for a permanent one, and the full-inventory text for the caps.
    /// </summary>
    public static CatalogPurchaseErrorType ErrorFor(EffectGrantResult result) =>
        result switch
        {
            EffectGrantResult.AlreadyPermanent => CatalogPurchaseErrorType.EffectOwned,
            EffectGrantResult.LimitReached => CatalogPurchaseErrorType.InventoryFull,
            _ => CatalogPurchaseErrorType.PurchaseFailed,
        };
}
