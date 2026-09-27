using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans.Concurrency;
using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Primitives.Players.Grains;

public partial interface IPlayerPresenceGrain
{
    /// <summary>What the player wears changed: the room they are in, or else they alone, is told.</summary>
    [AlwaysInterleave]
    public Task OnSelectedBadgesChangedAsync(
        ImmutableArray<PlayerBadgeSnapshot> selectedBadges,
        CancellationToken ct
    );
}
