namespace Turbo.Primitives.Camera;

/// <summary>
/// The camera's photos as the renderer drew them: the picture the photo dialog and a photo poster
/// load (Habbo's <c>stories.image_url_base</c> + the photo's url, its poster the <c>_small</c> copy).
/// </summary>
public interface ICameraPhotoFiles
{
    /// <summary>
    /// The photo's PNG on disk, by its file name without <c>.png</c> (<c>&lt;id&gt;</c> or
    /// <c>&lt;id&gt;_small</c>); null when there is none, or the name is not a photo's.
    /// </summary>
    string? GetPhotoFile(string name);
}
