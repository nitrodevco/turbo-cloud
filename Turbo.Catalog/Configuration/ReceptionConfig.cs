namespace Turbo.Catalog.Configuration;

/// <summary>What the reception's widgets are sent: its promo articles and community goals.</summary>
public sealed class ReceptionConfig
{
    /// <summary>Promo articles a player is sent at most. The client shows ten.</summary>
    public int PromoArticleLimit { get; init; } = 10;

    /// <summary>
    /// How long the articles and the community goal's totals are kept before they are read again,
    /// in seconds: how soon another silo sees an edit or a contribution.
    /// </summary>
    public int CacheSeconds { get; init; } = 30;
}
