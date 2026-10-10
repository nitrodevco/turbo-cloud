using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Turbo.PacketHandlers.Camera;

/// <summary>
/// The renders the camera was sent: each photo's and room thumbnail's render data, inflated and
/// written under <see cref="CameraConfig.StoragePath"/>, each player's last photo (what a purchase or
/// a publish is of), and the daily counts and publish times the limits are kept by.
/// </summary>
public sealed class CameraPhotoStore(IOptions<CameraConfig> config)
{
    private readonly CameraConfig _config = config.Value;
    private readonly ConcurrentDictionary<int, CameraPhoto> _lastPhotos = new();
    private readonly ConcurrentDictionary<(int PlayerId, string Kind, DateOnly Day), int> _counts =
        new();
    private readonly ConcurrentDictionary<int, DateTime> _lastPublished = new();

    /// <summary>
    /// The render data's JSON, or null when it is not zlib data or inflates past
    /// <see cref="CameraConfig.MaxRenderDataBytes"/>: a few KB of zlib can inflate to gigabytes,
    /// so it is read in pieces and given up on at the limit rather than read to the end.
    /// </summary>
    public string? Inflate(byte[] data)
    {
        try
        {
            using var input = new MemoryStream(data);
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[81920];
            int read;

            while ((read = zlib.Read(buffer)) > 0)
            {
                if (output.Length + read > _config.MaxRenderDataBytes)
                    return null;

                output.Write(buffer, 0, read);
            }

            return Encoding.UTF8.GetString(output.GetBuffer(), 0, (int)output.Length);
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    /// <summary>Counts one more render of a kind today; false once the day's limit is reached.</summary>
    public bool TryCount(int playerId, string kind, int limit)
    {
        var key = (playerId, kind, DateOnly.FromDateTime(DateTime.UtcNow));
        var count = _counts.AddOrUpdate(key, 1, (_, value) => value + 1);

        if (count <= limit)
            return true;

        _counts.AddOrUpdate(key, 0, (_, value) => value - 1);

        return false;
    }

    public async Task<CameraPhoto> SavePhotoAsync(int playerId, string json, CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString("N");
        var folder = Path.Combine(_config.StoragePath, "photos");

        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, $"{id}.json"), json, ct)
            .ConfigureAwait(false);

        var photo = new CameraPhoto(
            id,
            $"{_config.StorageUrlPrefix}{id}.png",
            DateTime.UtcNow,
            Path.Combine(folder, $"{id}.json"),
            Path.Combine(folder, $"{id}.png")
        );

        _lastPhotos[playerId] = photo;

        return photo;
    }

    /// <summary>Writes the thumbnail's render data; the JSON and PNG paths the renderer draws between.</summary>
    public async Task<(string JsonPath, string PngPath)> SaveThumbnailAsync(
        int roomId,
        string json,
        CancellationToken ct
    )
    {
        var folder = Path.Combine(_config.StoragePath, "thumbnails");
        var jsonPath = Path.Combine(folder, $"{roomId}.json");

        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(jsonPath, json, ct).ConfigureAwait(false);

        return (jsonPath, Path.Combine(folder, $"{roomId}.png"));
    }

    public CameraPhoto? GetLastPhoto(int playerId) =>
        _lastPhotos.TryGetValue(playerId, out var photo) ? photo : null;

    /// <summary>Seconds left before the player may publish again; 0 when they may.</summary>
    public int SecondsUntilPublish(int playerId)
    {
        if (!_lastPublished.TryGetValue(playerId, out var last))
            return 0;

        var left = _config.PublishCooldownSeconds - (DateTime.UtcNow - last).TotalSeconds;

        return left > 0 ? (int)Math.Ceiling(left) : 0;
    }

    public void MarkPublished(int playerId) => _lastPublished[playerId] = DateTime.UtcNow;
}

/// <summary>A stored photo render: its id, the url the client loads it from, when it was taken, and its JSON and PNG files.</summary>
public sealed record CameraPhoto(
    string Id,
    string Url,
    DateTime CreatedAt,
    string JsonPath,
    string PngPath
);
