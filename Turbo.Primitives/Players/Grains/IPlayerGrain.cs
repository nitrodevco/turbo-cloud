using System;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Players.Grains;

public interface IPlayerGrain : IGrainWithIntegerKey
{
    public Task SetOnlineStatusAsync(bool flag, CancellationToken ct);

    /// <summary>
    /// Records where the session that just opened came from: the address and the machine id,
    /// either of which may be unknown.
    /// </summary>
    public Task RecordLoginOriginAsync(string? address, string? machineId, CancellationToken ct);
    public Task SetFigureAsync(string figure, AvatarGenderType gender, CancellationToken ct);

    /// <summary>
    /// Fits what the player wears to what they may wear now - after their club ran out, or
    /// clothing was taken from them - changing it only when it must.
    /// </summary>
    public Task RefitFigureAsync(CancellationToken ct);
    public Task SetMottoAsync(string text, CancellationToken ct);

    /// <summary>
    /// The name a new player chose in the new user flow, once: written only while that step is
    /// still to do, which it then is not. Checking the name is the caller's; the database's unique
    /// name key is what refuses one taken meanwhile. The player's own name is accepted as is.
    /// </summary>
    public Task<NameCheckResult> ChooseNameAsync(string name, CancellationToken ct);
    public Task<PlayerSummarySnapshot> GetSummaryAsync(CancellationToken ct);

    /// <summary>Consumes one daily respect; false when none are left today.</summary>
    public Task<bool> TryUseRespectAsync(CancellationToken ct);

    /// <summary>Spends one of today's pet scratches; false when none are left.</summary>
    public Task<bool> TryUsePetRespectAsync(CancellationToken ct);

    /// <summary>Records a received respect and returns the new total.</summary>
    public Task<int> ReceiveRespectAsync(CancellationToken ct);

    /// <summary>Durably reserves a respect with an idempotent participant receipt.</summary>
    public Task<bool> SpendRespectOperationAsync(Guid respectId, CancellationToken ct);
    public Task<bool> SpendPetRespectOperationAsync(Guid respectId, CancellationToken ct);

    /// <summary>Durably applies the recipient's part of a respect operation once.</summary>
    public Task<int> ReceiveRespectOperationAsync(Guid respectId, CancellationToken ct);

    /// <summary>Refills today's respects if a replenish is available; false otherwise.</summary>
    public Task<bool> ReplenishRespectAsync(CancellationToken ct);

    /// <summary>
    /// The player's place on the total badges board, told by their inventory, which is where the
    /// badges are. A change is passed on to the room they are in.
    /// </summary>
    public Task SetBadgesRankAsync(int badgesRank, CancellationToken ct);

    /// <summary>Refreshes completed-award projections without calling back into achievements.</summary>
    [global::Orleans.Concurrency.AlwaysInterleave]
    public Task SetAchievementTotalsAsync(int score, int earnedLevels, CancellationToken ct);

    public Task<PlayerExtendedProfileSnapshot> GetExtendedProfileSnapshotAsync(
        CancellationToken ct
    );
}
