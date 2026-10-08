using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Sound.Snapshots;

namespace Turbo.Primitives.Sound.Grains;

/// <summary>
/// The song disks one jukebox holds, keyed by the jukebox's item id. A disk put in a jukebox
/// stays an ordinary furniture row, owned by whoever put it there, held by the jukebox and in
/// no inventory, in the playlist's order; taking it out hands it back to that owner. The
/// playlist goes wherever the jukebox goes: picking the jukebox up does not empty it.
/// <para>
/// The room awaits this grain; this grain awaits inventories and the song directory and never
/// a room, so it cannot wait on its caller. It sends nothing: the room tells its players.
/// </para>
/// </summary>
public interface IJukeboxGrain : IGrainWithIntegerKey
{
    /// <summary>The disks in the jukebox, in playing order.</summary>
    public Task<ImmutableArray<SongDiskSnapshot>> GetDisksAsync(CancellationToken ct);

    /// <summary>
    /// Takes a song disk out of the player's inventory into the playlist at
    /// <paramref name="slot"/> (clamped to the playlist, so the client's "at the end" lands at the
    /// end). Refused when the playlist is full, the player does not hold the disk, or it carries
    /// no song the hotel has.
    /// </summary>
    public Task<JukeboxChangeResultSnapshot> AddDiskAsync(
        PlayerId playerId,
        RoomObjectId diskId,
        int slot,
        CancellationToken ct
    );

    /// <summary>Takes the disk at <paramref name="slot"/> out and gives it back to its owner.</summary>
    public Task<JukeboxChangeResultSnapshot> RemoveDiskAsync(int slot, CancellationToken ct);
}
