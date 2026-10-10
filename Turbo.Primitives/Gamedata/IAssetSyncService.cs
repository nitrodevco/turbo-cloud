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
}
