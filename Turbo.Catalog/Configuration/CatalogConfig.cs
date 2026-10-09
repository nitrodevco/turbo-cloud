namespace Turbo.Catalog.Configuration;

public class CatalogConfig
{
    public const string SECTION_NAME = "Turbo:Catalog";

    /// <summary>
    /// The longest text engraved on a trophy, in characters: one bought from the trophy page, and
    /// a mystery trophy engraved in a room.
    /// </summary>
    public int TrophyInscriptionMaxLength { get; init; } = 100;

    /// <summary>
    /// Configuration for LTD raffle weighting criteria.
    /// </summary>
    public LtdRaffleWeightConfig LtdRaffle { get; set; } = new();

    /// <summary>What the Builders Club allows and how often its borrow counts are recounted.</summary>
    public BuildersClubConfig BuildersClub { get; init; } = new();

    /// <summary>What a gift can be wrapped in, and what wrapping costs.</summary>
    public GiftWrappingConfig GiftWrapping { get; init; } = new();

    /// <summary>The reception's promo articles and community goals.</summary>
    public ReceptionConfig Reception { get; init; } = new();

    /// <summary>How vouchers are redeemed and made.</summary>
    public VoucherConfig Vouchers { get; init; } = new();
}
