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

    /// <summary>The render data's JSON, or null when it is not zlib data.</summary>
    public static string? Inflate(byte[] data)
    {
        try
        {
            using var input = new MemoryStream(data);
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            using var reader = new StreamReader(zlib, Encoding.UTF8);

            return reader.ReadToEnd();
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

        var photo = new CameraPhoto(id, $"{_config.StorageUrlPrefix}{id}.png", DateTime.UtcNow);

        _lastPhotos[playerId] = photo;

        return photo;
    }

    public async Task SaveThumbnailAsync(int roomId, string json, CancellationToken ct)
    {
        var folder = Path.Combine(_config.StoragePath, "thumbnails");

        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, $"{roomId}.json"), json, ct)
            .ConfigureAwait(false);
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

/// <summary>A stored photo render: its id, the url the client loads it from, and when it was taken.</summary>
public sealed record CameraPhoto(string Id, string Url, DateTime CreatedAt);
