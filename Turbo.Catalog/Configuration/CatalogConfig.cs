namespace Turbo.Catalog.Configuration;

public class CatalogConfig
{
    public const string SECTION_NAME = "Turbo:Catalog";

    /// <summary>
    /// Configuration for LTD raffle weighting criteria.
    /// </summary>
    public LtdRaffleWeightConfig LtdRaffle { get; set; } = new();
}
