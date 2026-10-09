namespace Turbo.Primitives.Hotel.Enums;

/// <summary>
/// Where a promo article's button goes, as the client's <c>PromoArticleWidget</c> reads its
/// <c>linkType</c>.
/// </summary>
public enum PromoArticleLinkType
{
    /// <summary>A web page, opened outside the client; no button without an address.</summary>
    WebPage = 0,

    /// <summary>A client link (<c>catalog/open/...</c>, <c>navigator/goto/...</c>).</summary>
    ClientLink = 1,

    /// <summary>No button.</summary>
    None = 2,
}
