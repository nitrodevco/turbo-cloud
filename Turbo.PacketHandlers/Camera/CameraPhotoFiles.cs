using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Turbo.Primitives.Camera;

namespace Turbo.PacketHandlers.Camera;

/// <summary>
/// The photo PNGs under <see cref="CameraConfig.StoragePath"/>'s <c>photos/</c>, as
/// <see cref="CameraPhotoStore"/> names them, and each bought photo's extra data beside them, for
/// the gamedata host to serve. A name is a photo id (32 hex digits) with an optional <c>_small</c>,
/// so a request can name no other file.
/// </summary>
public sealed partial class CameraPhotoFiles(IOptions<CameraConfig> config) : ICameraPhotoFiles
{
    /// <summary>What a photo's extra data file is named after its id.</summary>
    public const string EXTRA_DATA_SUFFIX = ".extradata.json";

    private readonly CameraConfig _config = config.Value;

    public string? GetPhotoFile(string name)
    {
        if (!PhotoName().IsMatch(name))
            return null;

        var path = Path.Combine(_config.StoragePath, "photos", $"{name}.png");

        return File.Exists(path) ? path : null;
    }

    public string? GetExtraDataFile(string id)
    {
        if (!PhotoId().IsMatch(id))
            return null;

        var path = Path.Combine(_config.StoragePath, "photos", $"{id}{EXTRA_DATA_SUFFIX}");

        return File.Exists(path) ? path : null;
    }

    [GeneratedRegex("^[0-9a-f]{32}(_small)?$")]
    private static partial Regex PhotoName();

    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex PhotoId();
}
