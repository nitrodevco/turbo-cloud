namespace Turbo.Primitives.Catalog.Enums;

/// <summary>What brings a player closer to a bonus rare campaign's reward.</summary>
public enum BonusRareSource
{
    /// <summary>
    /// Credits bought with money, recorded by the hotel's purchase integration with a receipt of
    /// its own (<c>IBonusRareService.RecordPurchaseAsync</c>). Nothing else counts.
    /// </summary>
    PurchasedCredits = 0,

    /// <summary>Credits spent in the normal catalogue, as the server debited them.</summary>
    CatalogSpending = 1,
}
