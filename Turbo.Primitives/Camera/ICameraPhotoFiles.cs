namespace Turbo.Primitives.Camera;

/// <summary>
/// The camera's photos as the renderer drew them: the picture the photo dialog and a photo poster
/// load (Habbo's <c>stories.image_url_base</c> + the photo's url, its poster the <c>_small</c> copy),
/// and a bought photo's extra data, which a poster's viewer loads (Habbo's
/// <c>extra_data_service_url</c> + the photo's id).
/// </summary>
public interface ICameraPhotoFiles
{
    /// <summary>
    /// The photo's PNG on disk, by its file name without <c>.png</c> (<c>&lt;id&gt;</c> or
    /// <c>&lt;id&gt;_small</c>); null when there is none, or the name is not a photo's.
    /// </summary>
    string? GetPhotoFile(string name);

    /// <summary>
    /// The photo's extra data on disk, a JSON object (its <c>url</c>, <c>creator_name</c>,
    /// <c>creator_id</c>, <c>time</c>, ...), by the photo's id; null when the photo was never
    /// bought, or the id is not a photo's.
    /// </summary>
    string? GetExtraDataFile(string id);
}
