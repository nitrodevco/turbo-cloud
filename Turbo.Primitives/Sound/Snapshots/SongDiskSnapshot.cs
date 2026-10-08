using Orleans;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Sound.Snapshots;

/// <summary>
/// A song disk as the music packets name one: the disk's item id and the song on it. Both a
/// player's disks (<c>UserSongDisksInventory</c>) and a jukebox's playlist
/// (<c>JukeboxSongDisks</c>) are lists of these.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record SongDiskSnapshot
{
    [Id(0)]
    public required RoomObjectId DiskId { get; init; }

    [Id(1)]
    public required int SongId { get; init; }
}
