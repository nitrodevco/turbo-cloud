using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Orleans.Snapshots.Players;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Grains.Players;

public interface IPlayerGrain : IGrainWithIntegerKey
{
    public Task SetFigureAsync(string figure, AvatarGenderType gender, CancellationToken ct);
    public Task SetMottoAsync(string text, CancellationToken ct);
    public Task<PlayerSummarySnapshot> GetSummaryAsync(CancellationToken ct);

    public Task<PlayerExtendedProfileSnapshot> GetExtendedProfileSnapshotAsync(
        CancellationToken ct
    );

    /// <summary>
    /// Gives one respect point to this player from the giver.
    /// Returns the updated total respect count of this player.
    /// </summary>
    public Task<int> ReceiveRespectAsync(PlayerId giverId, CancellationToken ct);

    /// <summary>
    /// Consumes one "respect left" from this player (the giver).
    /// Returns false if no respects remain.
    /// </summary>
    public Task<bool> TryConsumeRespectAsync(CancellationToken ct);

    /// <summary>
    /// Replenishes respect to max per day. Consumes one replenish use.
    /// Returns false if no replenishes remain.
    /// </summary>
    public Task<bool> TryReplenishRespectAsync(int maxRespectPerDay, CancellationToken ct);

    /// <summary>
    /// Resets daily respect allowances for this player.
    /// Called at midnight server time.
    /// </summary>
    public Task ResetDailyRespectAsync(
        int dailyRespectAmount,
        int dailyPetRespectAmount,
        int dailyReplenishLimit,
        CancellationToken ct
    );
}
