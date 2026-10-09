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
}
