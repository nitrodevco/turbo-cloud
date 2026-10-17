using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Turbo.Primitives.Camera;

namespace Turbo.PacketHandlers.Camera;

/// <summary>
/// The photo PNGs under <see cref="CameraConfig.StoragePath"/>'s <c>photos/</c>, as
/// <see cref="CameraPhotoStore"/> names them, for the gamedata host to serve. A name is a photo
/// id (32 hex digits) with an optional <c>_small</c>, so a request can name no other file.
/// </summary>
public sealed partial class CameraPhotoFiles(IOptions<CameraConfig> config) : ICameraPhotoFiles
{
    private readonly CameraConfig _config = config.Value;

    public string? GetPhotoFile(string name)
    {
        if (!PhotoName().IsMatch(name))
            return null;

        var path = Path.Combine(_config.StoragePath, "photos", $"{name}.png");

        return File.Exists(path) ? path : null;
    }

    [GeneratedRegex("^[0-9a-f]{32}(_small)?$")]
    private static partial Regex PhotoName();
}
