namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// Where the client loads its images from, as the templates in its <c>nitro-config.json</c>, so
/// the panel shows what players see. Each keeps the client's own placeholders; an empty one shows
/// none.
/// </summary>
/// <param name="CatalogIcon"><c>catalog.icons.url</c>: a page icon, <c>%name%</c> its number.</param>
/// <param name="CatalogImage"><c>asset.urls.catalog</c>: a page image, <c>%name%</c> its name.</param>
/// <param name="FurniIcon">
/// <c>asset.urls.icons.furni</c>: a furniture icon, <c>%libname%</c> its class name and
/// <c>%param%</c> <c>_</c> and its colour, or nothing.
/// </param>
/// <param name="Badge"><c>badge.asset.url</c>: a badge, <c>%badgename%</c> its code.</param>
/// <param name="ImageLibrary">
/// <c>image.library.url</c>: the folder a catalog featured item's promo image is under, its
/// path appended.
/// </param>
/// <param name="Templates">
/// <c>asset.bundles.templates</c>: a library of the client's own UI, <c>%libname%</c> its name
/// (the chat bubbles' <c>habbo-free-flow-chat-com</c>).
/// </param>
/// <param name="Imager">
/// The avatar imager's address (<c>Turbo:Admin:ImagerUrl</c>), without its trailing slash; empty
/// without one.
/// </param>
/// <param name="Preview">
/// The client's preview host (<c>Turbo:Admin:ClientPreviewUrl</c>, else the page of
/// <c>ClientLoginUrl</c>, with <c>?preview</c>); empty without either.
/// </param>
public sealed record ClientAssetsResponse(
    string CatalogIcon,
    string CatalogImage,
    string FurniIcon,
    string Badge,
    string ImageLibrary,
    string Templates,
    string Imager,
    string Preview
)
{
    public static readonly ClientAssetsResponse NONE = new("", "", "", "", "", "", "", "");
}
