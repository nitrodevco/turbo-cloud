using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Turbo.Primitives.Rooms;

namespace Turbo.PacketHandlers.Camera;

/// <summary>
/// The room thumbnails: each room's last thumbnail render data and the PNG drawn from it, one pair
/// per room under <see cref="CameraConfig.StoragePath"/>'s <c>thumbnails/</c>, a new one replacing
/// the old. The gamedata host serves the PNG (<see cref="IRoomThumbnailFiles"/>).
/// </summary>
public sealed class RoomThumbnailStore(IOptions<CameraConfig> config) : IRoomThumbnailFiles
{
    private readonly CameraConfig _config = config.Value;

    private string Folder => Path.Combine(_config.StoragePath, "thumbnails");

    /// <summary>Writes the thumbnail's render data; the JSON and PNG paths the renderer draws between.</summary>
    public async Task<(string JsonPath, string PngPath)> SaveAsync(
        int roomId,
        string json,
        CancellationToken ct
    )
    {
        var jsonPath = Path.Combine(Folder, $"{roomId}.json");

        Directory.CreateDirectory(Folder);
        await File.WriteAllTextAsync(jsonPath, json, ct).ConfigureAwait(false);

        return (jsonPath, PngPath(roomId));
    }

    public string? GetThumbnailFile(RoomId roomId)
    {
        if (roomId.Value <= 0)
            return null;

        var path = PngPath(roomId.Value);

        return File.Exists(path) ? path : null;
    }

    private string PngPath(int roomId) => Path.Combine(Folder, $"{roomId}.png");
}
