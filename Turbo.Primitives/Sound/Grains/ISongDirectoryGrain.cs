using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Orleans.Concurrency;
using Turbo.Primitives.Sound.Snapshots;

namespace Turbo.Primitives.Sound.Grains;

/// <summary>
/// Every trax song in the hotel (<c>songs</c>), one grain. Clients ask for songs by id whenever a
/// disk, a playlist or a catalog page shows one, and jukeboxes ask for their lengths, so reads
/// answer from memory and interleave; staff add, change and remove songs here, written through.
/// </summary>
public interface ISongDirectoryGrain : IGrainWithStringKey
{
    /// <summary>The songs of these ids, in the order asked; unknown ids are left out.</summary>
    [AlwaysInterleave]
    public Task<ImmutableArray<SongSnapshot>> GetSongsAsync(
        ImmutableArray<int> songIds,
        CancellationToken ct
    );

    /// <summary>The id of the song sold under this catalog code, or null when no song has it.</summary>
    [AlwaysInterleave]
    public Task<int?> GetSongIdByCodeAsync(string code, CancellationToken ct);

    /// <summary>Every song, by id, for the admin panel.</summary>
    [AlwaysInterleave]
    public Task<ImmutableArray<SongSnapshot>> GetAllSongsAsync(CancellationToken ct);

    /// <summary>
    /// How many song disks carry each song, counted from the furniture rows; songs with none are
    /// left out. A query per call, for the admin panel only.
    /// </summary>
    [AlwaysInterleave]
    public Task<ImmutableDictionary<int, int>> CountDisksAsync(CancellationToken ct);

    public Task<SongEditResult> CreateSongAsync(SongDraft draft, CancellationToken ct);

    public Task<SongEditResult> UpdateSongAsync(int songId, SongDraft draft, CancellationToken ct);

    /// <summary>Removes a song no disk carries; a song on a disk is refused.</summary>
    public Task<SongEditResult> DeleteSongAsync(int songId, CancellationToken ct);
}
