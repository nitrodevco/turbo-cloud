namespace Turbo.PacketHandlers.Camera;

/// <summary>
/// The camera's prices and limits (<c>InitCameraMessage</c>) and where its renders are kept.
/// The prices are Habbo's (the "Buy or publish" dialog: a poster 2 credits, publishing 10 duckets);
/// the limits are this hotel's choice.
/// </summary>
public sealed class CameraConfig
{
    public const string SECTION_NAME = "Turbo:Camera";

    public int CreditPrice { get; init; } = 2;

    /// <summary>The poster's ducket price; 0 hides it in the dialog.</summary>
    public int DucketPrice { get; init; }

    public int PublishDucketPrice { get; init; } = 10;

    /// <summary>The folder the render data is written to: <c>photos/</c> and <c>thumbnails/</c> under it.</summary>
    public string StoragePath { get; init; } = "camera";

    /// <summary>What <c>CameraStorageUrlMessage</c> sends before a photo's file name; the client puts <c>stories.image_url_base</c> in front.</summary>
    public string StorageUrlPrefix { get; init; } = "photos/";

    /// <summary>The photo furni a purchase makes (Habbo's "Camera Pic" poster).</summary>
    public string PhotoFurnitureName { get; init; } = "external_image_wallitem_poster_small";

    /// <summary>Photo renders per player per day; past it the storage url is empty (<c>camera.render.count.info</c>).</summary>
    public int RenderLimitPerDay { get; init; } = 100;

    /// <summary>Seconds between two published photos (<c>camera.publish.wait</c>).</summary>
    public int PublishCooldownSeconds { get; init; } = 60;

    /// <summary>Room thumbnails per player per day (<c>ThumbnailStatusMessage.isRenderLimitHit</c>).</summary>
    public int ThumbnailLimitPerDay { get; init; } = 50;

    /// <summary>What runs the renderer (<see cref="CameraRenderer"/>); empty leaves the render JSON undrawn.</summary>
    public string RendererCommand { get; init; } = "node";

    /// <summary>The renderer script, relative to the server's folder: it gets the JSON path, the PNG path, <c>--cache</c> and <c>--furni-url</c>.</summary>
    public string RendererScript { get; init; } = "tools/camera-renderer/render.mjs";

    /// <summary>Where the renderer keeps the furniture bundles it downloaded, relative to the server's folder.</summary>
    public string RendererCacheDirectory { get; init; } = "camera/cache";

    /// <summary>The furniture bundles (the client's <c>asset.urls.furni</c>, <c>%libname%</c> for the library); empty uses the script's default.</summary>
    public string RendererFurniUrl { get; init; } = "";

    /// <summary>
    /// The url prefixes an external image in a render (a sprite named <c>http...</c>: an image
    /// library picture, a group badge, another photo) may be fetched from, each ending in <c>/</c>.
    /// The render data is the client's, so any other url is left undrawn rather than fetched by
    /// the server; empty draws no external image.
    /// </summary>
    public string[] RendererExternalImageUrls { get; init; } = [];

    /// <summary>
    /// The most render data, inflated, a photo or thumbnail may be: a render is a list of sprites,
    /// a few hundred KB at most, so more is not the client's and is refused before it is held whole.
    /// </summary>
    public int MaxRenderDataBytes { get; init; } = 2 * 1024 * 1024;

    /// <summary>Renders run at once across the server; past it a render waits, within its timeout.</summary>
    public int MaxConcurrentRenders { get; init; } = 2;

    /// <summary>How long one render may take before it is killed and the photo stays undrawn.</summary>
    public int RendererTimeoutMilliseconds { get; init; } = 15000;
}
