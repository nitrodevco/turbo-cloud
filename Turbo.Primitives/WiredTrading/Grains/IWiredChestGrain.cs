using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Primitives.WiredTrading.Grains;

/// <summary>
/// What one wired chest holds, keyed by the chest's item id: the furni rows stored in it and its
/// credits, and every move in or out, logged in the same database transaction as the move.
/// <para>
/// The room awaits this grain; this grain awaits only inventories and wallets and never a room,
/// so it cannot wait on its caller. It tells the players looking inside what changed.
/// </para>
/// </summary>
public interface IWiredChestGrain : IGrainWithIntegerKey
{
    public Task<WiredChestSummarySnapshot> GetSummaryAsync(
        WiredChestSettingsSnapshot chest,
        CancellationToken ct
    );

    /// <summary>
    /// Sends the contents to a player and keeps them up to date until they close the window.
    /// Returns how many players are looking now; the same count when the viewer cap refused this one.
    /// </summary>
    public Task<int> OpenAsync(
        WiredChestSettingsSnapshot chest,
        PlayerId viewerId,
        CancellationToken ct
    );

    /// <summary>Stops updating a player. Returns how many players are still looking.</summary>
    public Task<int> CloseAsync(PlayerId viewerId, CancellationToken ct);

    public Task<WiredChestMoveResultSnapshot> DepositAsync(
        WiredChestDepositRequest request,
        CancellationToken ct
    );

    public Task<WiredChestMoveResultSnapshot> WithdrawAsync(
        WiredChestWithdrawRequest request,
        CancellationToken ct
    );

    /// <summary>
    /// Buys capacity: the payer is charged per upgrade, then the level goes up. A starter chest
    /// and one already at its maximum are refused before anything is charged.
    /// </summary>
    public Task<(UpgradeChestResultType Result, WiredChestSummarySnapshot Summary)> UpgradeAsync(
        WiredChestSettingsSnapshot chest,
        PlayerId payerId,
        int upgrades,
        CancellationToken ct
    );
}
