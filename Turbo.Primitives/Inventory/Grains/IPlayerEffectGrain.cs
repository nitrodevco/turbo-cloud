using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Orleans.Concurrency;
using Turbo.Primitives.Inventory.Snapshots;

namespace Turbo.Primitives.Inventory.Grains;

/// <summary>
/// The avatar effects one player owns, keyed by player id. A player holds copies of an effect,
/// activates one to start its timer, wears an effect they have running, and the running copy
/// expires on its own. Its own grain, not a section of the inventory, for the reason the badges
/// are: it is asked at every login and must not queue behind furniture work.
/// <para>
/// Ownership and time are this grain's; what the avatar looks like is the room's. Wearing is
/// told to the presence, which tells the room, and the room refuses it while the avatar wears
/// something the hotel put on it (riding, a game team, a freeze).
/// </para>
/// </summary>
public interface IPlayerEffectGrain : IGrainWithIntegerKey
{
    /// <summary>
    /// The list the client's effects tab shows, one entry per effect id: copies waiting, and how
    /// long the running copy has left (<c>-1</c> when none runs). Sent at login.
    /// </summary>
    [AlwaysInterleave]
    public Task<ImmutableArray<AvatarEffectSnapshot>> GetEffectsAsync(CancellationToken ct);

    /// <summary>
    /// What giving all of these (timed copies) would answer, changing nothing: the first refusal,
    /// or <see cref="EffectGrantResult.Granted"/>. The catalog asks before it charges, so a
    /// purchase that cannot be delivered is refused instead of refunded. The requests are judged
    /// together, so two new effects that would each fit but not both are refused.
    /// </summary>
    [AlwaysInterleave]
    public Task<EffectGrantResult> CheckGiveEffectsAsync(
        ImmutableArray<EffectGrantRequest> requests,
        CancellationToken ct
    );

    /// <summary>
    /// Gives the player <paramref name="copies"/> waiting copies of an effect, or the effect for
    /// good. The player is told. A permanent effect replaces the copies the player holds.
    /// </summary>
    public Task<EffectGrantResult> GiveEffectAsync(
        int effectId,
        int subType,
        int copies,
        bool permanent,
        CancellationToken ct
    );

    /// <summary>
    /// The client's activate: starts one waiting copy of the effect and wears it. An effect that
    /// is already running or permanent is only worn, so a repeated request costs nothing. False
    /// when the player has no such effect.
    /// </summary>
    public Task<bool> ActivateEffectAsync(int effectId, CancellationToken ct);

    /// <summary>
    /// The client's select: wears an effect the player has running, or, for an id of zero or
    /// less, takes the worn effect off. False when the effect is not one they have running.
    /// </summary>
    public Task<bool> SelectEffectAsync(int effectId, CancellationToken ct);
}
