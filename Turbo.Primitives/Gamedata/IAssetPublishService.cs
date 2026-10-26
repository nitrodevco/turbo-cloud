using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Gamedata;

/// <summary>
/// The places bundles are published to (a folder on this server, or an FTP, FTPS or SFTP server),
/// and publishing to them: only what a target lacks or holds an older copy of is sent, and what is
/// sent is recorded as it goes, so a publish that stops resumes where it was. Changes refuse bad
/// input with <see cref="System.ArgumentException"/>, and a clash with what is running or saved
/// with <see cref="System.InvalidOperationException"/>; both carry words the panel can show.
/// </summary>
public interface IAssetPublishService
{
    Task<IReadOnlyList<AssetPublishTargetSnapshot>> ListTargetsAsync(CancellationToken ct);

    /// <summary>The target; null when there is no such target.</summary>
    Task<AssetPublishTargetSnapshot?> GetTargetAsync(int targetId, CancellationToken ct);

    Task<AssetPublishTargetSnapshot> CreateTargetAsync(
        AssetPublishTargetEdit edit,
        CancellationToken ct
    );

    /// <summary>Null when there is no such target.</summary>
    Task<AssetPublishTargetSnapshot?> UpdateTargetAsync(
        int targetId,
        AssetPublishTargetEdit edit,
        CancellationToken ct
    );

    /// <summary>Removes the target with what it recorded and its history; false when there is no such target.</summary>
    Task<bool> DeleteTargetAsync(int targetId, CancellationToken ct);

    /// <summary>
    /// A page (from 0) of what the target lacks or holds an older copy of, by what it recorded,
    /// whose path holds <paramref name="query"/>, with why each is sent; null when there is no
    /// such target. Files gone from the target are found only by a publish (a dry run names them).
    /// </summary>
    Task<AssetPublishPending?> GetPendingAsync(
        int targetId,
        string? query,
        int page,
        CancellationToken ct
    );

    /// <summary>
    /// What changed in the files the target holds an older copy of: the copy it holds, read through
    /// it (and kept, by its path and recorded hash, until it is sent again), against the hotel's.
    /// <paramref name="path"/> compares that one file, a pending one or not; without it, every file
    /// pending as changed, up to a limit. Null when there is no such target, or the hotel has no file
    /// at <paramref name="path"/>.
    /// </summary>
    Task<AssetPublishComparisons?> CompareAsync(int targetId, string? path, CancellationToken ct);

    /// <summary>
    /// An image a comparison names, as the target holds it (<paramref name="fromTarget"/>) or as the
    /// hotel does: the file at <paramref name="path"/>, or the <paramref name="entry"/> in that
    /// bundle. Null when there is no such target, file or image.
    /// </summary>
    Task<byte[]?> ReadComparedImageAsync(
        int targetId,
        string path,
        string? entry,
        bool fromTarget,
        CancellationToken ct
    );

    /// <summary>Connects (within the configured time) and lists its folder; null when there is no such target.</summary>
    Task<AssetPublishTestResult?> TestAsync(int targetId, CancellationToken ct);

    /// <summary>
    /// Starts publishing to the target as an asset job and returns it; null when there is no such
    /// target. A dry run only counts and names what it would send; <paramref name="deleteRemoved"/>
    /// also deletes from the target what it was sent that the hotel no longer has.
    /// </summary>
    Task<AssetJobSnapshot?> PublishAsync(
        int targetId,
        bool dryRun,
        bool deleteRemoved,
        PlayerId player,
        CancellationToken ct
    );

    /// <summary>Forgets the SFTP host key trusted, so the next connection trusts the one it is shown; false when there is no such target.</summary>
    Task<bool> ForgetHostKeyAsync(int targetId, CancellationToken ct);

    /// <summary>The target's publishes, newest first; null when there is no such target.</summary>
    Task<IReadOnlyList<AssetPublishSnapshot>?> GetHistoryAsync(int targetId, CancellationToken ct);
}
