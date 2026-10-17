using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Gamedata;

/// <summary>
/// Takes Habbo's furniture, effect and pet libraries the hotel lacks, or has an older revision of,
/// and converts them to bundles, as an asset job (<see cref="IAssetJobs"/>). An uploaded bundle is
/// never replaced.
/// </summary>
public interface IAssetSyncService
{
    /// <summary>
    /// Starts a sync and returns its job at once. Throws <see cref="System.InvalidOperationException"/>
    /// while another asset job runs.
    /// </summary>
    AssetJobSnapshot Start(PlayerId player);

    /// <summary>
    /// Starts a sync after a Habbo check found what Habbo serves, when <c>Turbo:Assets:SyncAfterCheck</c>
    /// is on and no asset job runs. Null when it did not start; the check stands either way.
    /// </summary>
    AssetJobSnapshot? StartAfterCheck(PlayerId player);

    /// <summary>
    /// Starts building the client's libraries from the release of Habbo's HTML client followed -
    /// with <paramref name="force"/> every one again, else those whose library changed - and returns
    /// its job at once. Throws <see cref="System.InvalidOperationException"/> while another asset job runs.
    /// </summary>
    AssetJobSnapshot StartClientBuild(PlayerId player, bool force);

    /// <summary>
    /// What a sync would download now, without downloading it: Habbo's lists, read again when
    /// <paramref name="relist"/> or when the last read is older than
    /// <c>Turbo:Assets:SyncPreviewMinutes</c>, against the hotel's bundles as they are. Throws
    /// <see cref="System.Net.Http.HttpRequestException"/> when Habbo's variables or furniture data
    /// can't be read.
    /// </summary>
    Task<AssetSyncPreview> PreviewAsync(bool relist, CancellationToken ct);
}
