using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Catalog.Snapshots;

namespace Turbo.Primitives.Catalog.Grains;

/// <summary>
/// Grain that manages the LTD raffle system for a specific series.
/// Keyed by LTD Series ID.
/// </summary>
public interface ILtdRaffleGrain : IGrainWithIntegerKey
{
    /// <summary>
    /// Player enters the raffle for this LTD series.
    /// </summary>
    /// <param name="playerId">The player attempting to enter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure reason.</returns>
    Task<LtdRaffleEntryResult> EnterRaffleAsync(int playerId, CancellationToken ct);

    /// <summary>
    /// Get the current series snapshot.
    /// </summary>
    Task<LtdSeriesSnapshot?> GetSeriesSnapshotAsync(CancellationToken ct);

    /// <summary>
    /// Admin: Force run the raffle for the current batch immediately.
    /// </summary>
    Task ForceRunRaffleAsync(CancellationToken ct);

    /// <summary>
    /// Admin: Reload series configuration from database.
    /// </summary>
    Task ReloadSeriesAsync(CancellationToken ct);
}
