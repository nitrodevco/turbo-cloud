using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Players.Grains;

public interface IPlayerGrain : IGrainWithIntegerKey
{
    public Task SetOnlineStatusAsync(bool flag, CancellationToken ct);
    public Task SetFigureAsync(string figure, AvatarGenderType gender, CancellationToken ct);
    public Task SetMottoAsync(string text, CancellationToken ct);
    public Task<PlayerSummarySnapshot> GetSummaryAsync(CancellationToken ct);

    /// <summary>Consumes one daily respect; false when none are left today.</summary>
    public Task<bool> TryUseRespectAsync(CancellationToken ct);

    /// <summary>Spends one of today's pet scratches; false when none are left.</summary>
    public Task<bool> TryUsePetRespectAsync(CancellationToken ct);

    /// <summary>Records a received respect and returns the new total.</summary>
    public Task<int> ReceiveRespectAsync(CancellationToken ct);

    /// <summary>Refills today's respects if a replenish is available; false otherwise.</summary>
    public Task<bool> ReplenishRespectAsync(CancellationToken ct);

    /// <summary>
    /// The player's place on the total badges board, told by their inventory, which is where the
    /// badges are. A change is passed on to the room they are in.
    /// </summary>
    public Task SetBadgesRankAsync(int badgesRank, CancellationToken ct);

    public Task<PlayerExtendedProfileSnapshot> GetExtendedProfileSnapshotAsync(
        CancellationToken ct
    );
}
