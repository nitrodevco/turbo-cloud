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

    /// <summary>
    /// The effect the player wears from their effects changed: the room they are in is told, and
    /// no one else, since an effect is only seen in a room. <paramref name="effectId"/> of zero
    /// takes off whichever of <paramref name="ownedEffectIds"/> the avatar wears. The room acts
    /// only where the avatar is bare or wears one of those, so an effect the hotel applied
    /// (riding, a game team, a freeze) is left alone. Nothing is remembered between calls: the
    /// avatar is the record of what is worn.
    /// </summary>
    [AlwaysInterleave]
    public Task OnWornEffectChangedAsync(
        int effectId,
        ImmutableArray<int> ownedEffectIds,
        CancellationToken ct
    );
}
