using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;
using Turbo.Database.Context;
using Turbo.Database.Entities.Players;
using Turbo.Inventory.Configuration;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Inventory.Avatareffect;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Grains;

namespace Turbo.Inventory.Grains.Effects;

/// <summary>
/// The avatar effects a player owns. Write-through: a row is written first and memory follows,
/// so nothing is flushed on deactivation. A copy that runs out is found by an absolute time
/// (<see cref="PlayerEffectEntity.ExpiresAt"/>), which keeps counting while the player is
/// offline and across restarts.
/// <para>
/// One timer, set to the earliest expiry and set again after each, tells the player when a copy
/// runs out. It is not what makes the effect expire: every call looks for lapsed copies first, a
/// list shows a lapsed copy as gone, and a grain that comes back after a long idle spell drops
/// whatever ran out meanwhile, so a late or lost tick only delays the message.
/// </para>
/// <para>
/// What the avatar wears is the room's, not this grain's. Wearing is told to the presence with
/// the effects the player owns, and the room acts only where the avatar is bare or wears one of
/// those; riding, a game team and a freeze put effects on avatars that this grain must not undo.
/// Nothing about what is worn is kept here, so it cannot go stale when the grain deactivates.
/// </para>
/// </summary>
internal sealed class PlayerEffectGrain : Grain, IPlayerEffectGrain
{
    /// <summary>A timer cannot be set far out; a longer wait is made in steps of this.</summary>
    private static readonly TimeSpan MAX_TIMER_DELAY = TimeSpan.FromHours(24);

    private static readonly TimeSpan RETRY_DELAY = TimeSpan.FromSeconds(30);

    /// <summary>How long past the expiry the grain is kept from deactivating, so its tick can run.</summary>
    private static readonly TimeSpan DEACTIVATION_MARGIN = TimeSpan.FromMinutes(1);

    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly EffectConfig _config;
    private readonly IGrainFactory _grainFactory;
    private readonly TimeProvider _time;
    private readonly ILogger<IPlayerEffectGrain> _logger;

    private readonly PlayerEffectLiveState _state;

    private IDisposable? _expiryTimer;

    private IPlayerPresenceGrain Presence => _grainFactory.GetPlayerPresenceGrain(_state.PlayerId);

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    public PlayerEffectGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<EffectConfig> config,
        IGrainFactory grainFactory,
        TimeProvider time,
        ILogger<IPlayerEffectGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _config = config.Value;
        _grainFactory = grainFactory;
        _time = time;
        _logger = logger;

        _state = new() { PlayerId = this.GetPlayerId() };
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            await HydrateAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to hydrate the effects of player {PlayerId}",
                _state.PlayerId
            );

            throw;
        }
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        _expiryTimer?.Dispose();
        _expiryTimer = null;

        return Task.CompletedTask;
    }

    public Task<ImmutableArray<AvatarEffectSnapshot>> GetEffectsAsync(CancellationToken ct) =>
        Task.FromResult(BuildSnapshots());

    public Task<EffectGrantResult> CheckGiveEffectAsync(
        int effectId,
        int copies,
        bool permanent,
        CancellationToken ct
    ) => Task.FromResult(Evaluate(effectId, copies, permanent));

    public async Task<EffectGrantResult> GiveEffectAsync(
        int effectId,
        int subType,
        int copies,
        bool permanent,
        CancellationToken ct
    )
    {
        // A copy that has run out is gone before the cap is worked out against it.
        await ExpireDueAsync(notify: true, ct);

        var result = Evaluate(effectId, copies, permanent);

        if (result != EffectGrantResult.Granted)
            return result;

        var storedSubType = _config.GetSubType(effectId, subType);

        if (!_state.EffectsById.TryGetValue(effectId, out var effect))
        {
            var entity = new PlayerEffectEntity
            {
                PlayerEntityId = _state.PlayerId.Value,
                EffectId = effectId,
                SubType = storedSubType,
                InactiveCount = permanent ? 0 : copies,
                IsPermanent = permanent,
                ExpiresAt = null,
                PlayerEntity = null!,
            };

            await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
            {
                dbCtx.PlayerEffects.Add(entity);

                await dbCtx.SaveChangesAsync(ct);
            }

            effect = new OwnedEffect(entity.Id, effectId)
            {
                SubType = storedSubType,
                InactiveCount = entity.InactiveCount,
                IsPermanent = permanent,
            };

            _state.EffectsById[effectId] = effect;

            await SendAddedAsync(effect, permanent ? 1 : copies, ct);

            return EffectGrantResult.Granted;
        }

        if (permanent)
        {
            // The client adds a copy to what it holds and cannot turn a stack into a permanent
            // effect, so it is sent the list again.
            await UpdateAsync(effect, 0, true, null, ct);

            ArmTimer();

            await SendEffectsAsync(ct);

            return EffectGrantResult.Granted;
        }

        await UpdateAsync(effect, effect.InactiveCount + copies, false, effect.ExpiresAt, ct);
        await SendAddedAsync(effect, copies, ct);

        return EffectGrantResult.Granted;
    }

    public async Task<bool> ActivateEffectAsync(int effectId, CancellationToken ct)
    {
        await ExpireDueAsync(notify: true, ct);

        if (!_state.EffectsById.TryGetValue(effectId, out var effect))
            return false;

        // One that already runs, or lasts for good, is only worn: the client sends activate
        // again on every room it enters, and that must not use up another copy.
        if (!effect.IsPermanent && effect.ExpiresAt is null)
        {
            if (effect.InactiveCount < 1)
                return false;

            var duration = _config.GetDurationSeconds(effectId);

            await UpdateAsync(
                effect,
                effect.InactiveCount - 1,
                false,
                Now.AddSeconds(duration),
                ct
            );

            await _grainFactory.SendComposerToPlayerAsync(
                _state.PlayerId,
                new AvatarEffectActivatedMessageComposer
                {
                    Type = effectId,
                    Duration = duration,
                    IsPermanent = false,
                },
                ct
            );

            ArmTimer();
        }

        // The client takes activating to mean wearing and sends the select straight after, in
        // whichever order the two arrive; wearing is repeatable.
        await Presence.OnWornEffectChangedAsync(effectId, OwnedEffectIds(), ct);

        return true;
    }

    public async Task<bool> SelectEffectAsync(int effectId, CancellationToken ct)
    {
        await ExpireDueAsync(notify: true, ct);

        // The official client sends -1 to take an effect off, Nitro sends 0.
        if (effectId <= 0)
        {
            await Presence.OnWornEffectChangedAsync(0, OwnedEffectIds(), ct);

            return true;
        }

        if (
            !_state.EffectsById.TryGetValue(effectId, out var effect)
            || !(effect.IsPermanent || effect.ExpiresAt is not null)
        )
            return false;

        await Presence.OnWornEffectChangedAsync(effectId, OwnedEffectIds(), ct);

        return true;
    }

    /// <summary>The expiry timer: tells the player about every copy that has run out.</summary>
    private async Task OnExpiryTimerAsync(CancellationToken ct)
    {
        try
        {
            await ExpireDueAsync(notify: true, ct);
        }
        catch (Exception ex)
        {
            // The schedule must outlive a failed write, or the player would never be told.
            _logger.LogError(
                ex,
                "Failed to expire the effects of player {PlayerId}",
                _state.PlayerId
            );

            ArmTimer(RETRY_DELAY);
        }
    }

    /// <summary>
    /// Ends every running copy whose time has come. A stack keeps its other copies, waiting; the
    /// last copy of a timed effect takes the row with it. <paramref name="notify"/> is false
    /// when the grain loads and finds copies that ran out while it was away: the list sent next
    /// is already right, and nobody has been shown them running.
    /// </summary>
    private async Task ExpireDueAsync(bool notify, CancellationToken ct)
    {
        var now = Now;
        var lapsed = _state
            .EffectsById.Values.Where(x => x.ExpiresAt is { } at && at <= now)
            .ToList();

        foreach (var effect in lapsed)
        {
            if (effect.InactiveCount == 0 && !effect.IsPermanent)
            {
                await DeleteAsync(effect, ct);

                _state.EffectsById.Remove(effect.EffectId);
            }
            else
            {
                await UpdateAsync(effect, effect.InactiveCount, effect.IsPermanent, null, ct);
            }

            if (!notify)
                continue;

            await _grainFactory.SendComposerToPlayerAsync(
                _state.PlayerId,
                new AvatarEffectExpiredMessageComposer { Type = effect.EffectId },
                ct
            );

            // Only the effect that ran out: another the player wears must stay on.
            await Presence.OnWornEffectChangedAsync(0, [effect.EffectId], ct);
        }

        ArmTimer();
    }

    private async Task HydrateAsync(CancellationToken ct)
    {
        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            var entities = await dbCtx
                .PlayerEffects.AsNoTracking()
                .Where(x => x.PlayerEntityId == _state.PlayerId.Value)
                .ToListAsync(ct);

            _state.EffectsById.Clear();

            foreach (var entity in entities)
                _state.EffectsById[entity.EffectId] = new OwnedEffect(entity.Id, entity.EffectId)
                {
                    SubType = entity.SubType,
                    InactiveCount = entity.InactiveCount,
                    IsPermanent = entity.IsPermanent,
                    ExpiresAt = entity.ExpiresAt,
                };
        }

        await ExpireDueAsync(notify: false, ct);
    }

    /// <summary>What <see cref="GiveEffectAsync"/> would answer; changes nothing.</summary>
    private EffectGrantResult Evaluate(int effectId, int copies, bool permanent)
    {
        if (effectId < 1 || effectId > _config.MaxEffectId)
            return EffectGrantResult.Invalid;

        if (!permanent && copies < 1)
            return EffectGrantResult.Invalid;

        if (!_state.EffectsById.TryGetValue(effectId, out var existing))
            return
                _state.EffectsById.Count >= _config.MaxDistinctEffects
                || (!permanent && copies > _config.MaxCopiesPerType)
                ? EffectGrantResult.LimitReached
                : EffectGrantResult.Granted;

        if (existing.IsPermanent)
            return EffectGrantResult.AlreadyPermanent;

        return !permanent && existing.InactiveCount + copies > _config.MaxCopiesPerType
            ? EffectGrantResult.LimitReached
            : EffectGrantResult.Granted;
    }

    private ImmutableArray<int> OwnedEffectIds() => [.. _state.EffectsById.Keys];

    private ImmutableArray<AvatarEffectSnapshot> BuildSnapshots()
    {
        var now = Now;

        return
        [
            .. _state
                .EffectsById.Values.OrderBy(x => x.EffectId)
                .Select(x => ToSnapshot(x, now))
                .OfType<AvatarEffectSnapshot>(),
        ];
    }

    /// <summary>
    /// The effect as the client lists it, or null when nothing of it is left to show. The client
    /// reads <c>-1</c> as "not running" and anything from zero up as "running", and divides by
    /// the duration, so neither may be 0. A running copy never shows less than a second, because
    /// Nitro's own test for running is "more than zero".
    /// </summary>
    private AvatarEffectSnapshot? ToSnapshot(OwnedEffect effect, DateTime now)
    {
        var duration = _config.GetDurationSeconds(effect.EffectId);

        if (effect.IsPermanent)
            return new AvatarEffectSnapshot
            {
                Type = effect.EffectId,
                SubType = effect.SubType,
                Duration = duration,
                InactiveEffectsInInventory = 0,
                SecondsLeftIfActive = duration,
                IsPermanent = true,
            };

        var running = effect.ExpiresAt is { } at && at > now;

        // A copy that ran out and is not yet cleared reads as gone, whatever the timer has done.
        if (!running && effect.InactiveCount < 1)
            return null;

        return new AvatarEffectSnapshot
        {
            Type = effect.EffectId,
            SubType = effect.SubType,
            Duration = duration,
            InactiveEffectsInInventory = effect.InactiveCount,
            SecondsLeftIfActive = running
                ? Math.Max(1, (int)Math.Ceiling((effect.ExpiresAt!.Value - now).TotalSeconds))
                : -1,
            IsPermanent = false,
        };
    }

    private async Task UpdateAsync(
        OwnedEffect effect,
        int inactiveCount,
        bool isPermanent,
        DateTime? expiresAt,
        CancellationToken ct
    )
    {
        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            await dbCtx
                .PlayerEffects.Where(x =>
                    x.Id == effect.RowId && x.PlayerEntityId == _state.PlayerId.Value
                )
                .ExecuteUpdateAsync(
                    s =>
                        s.SetProperty(x => x.InactiveCount, inactiveCount)
                            .SetProperty(x => x.IsPermanent, isPermanent)
                            .SetProperty(x => x.ExpiresAt, expiresAt),
                    ct
                );
        }

        effect.InactiveCount = inactiveCount;
        effect.IsPermanent = isPermanent;
        effect.ExpiresAt = expiresAt;
    }

    private async Task DeleteAsync(OwnedEffect effect, CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        await dbCtx
            .PlayerEffects.Where(x =>
                x.Id == effect.RowId && x.PlayerEntityId == _state.PlayerId.Value
            )
            .ExecuteDeleteAsync(ct);
    }

    /// <summary>One message per copy: the client counts each as one more of the stack.</summary>
    private async Task SendAddedAsync(OwnedEffect effect, int copies, CancellationToken ct)
    {
        for (var i = 0; i < copies; i++)
            await _grainFactory.SendComposerToPlayerAsync(
                _state.PlayerId,
                new AvatarEffectAddedMessageComposer
                {
                    Type = effect.EffectId,
                    SubType = effect.SubType,
                    Duration = _config.GetDurationSeconds(effect.EffectId),
                    IsPermanent = effect.IsPermanent,
                },
                ct
            );
    }

    private Task SendEffectsAsync(CancellationToken ct) =>
        _grainFactory.SendComposerToPlayerAsync(
            _state.PlayerId,
            new AvatarEffectsMessageComposer { Effects = BuildSnapshots() },
            ct
        );

    /// <summary>
    /// Sets the one timer to the earliest running copy, or to <paramref name="delay"/>. The grain
    /// is kept from deactivating until then, because a timer does not keep a grain alive.
    /// </summary>
    private void ArmTimer(TimeSpan? delay = null)
    {
        _expiryTimer?.Dispose();
        _expiryTimer = null;

        if (delay is null)
        {
            var earliest = _state
                .EffectsById.Values.Where(x => x.ExpiresAt is not null)
                .Select(x => x.ExpiresAt!.Value)
                .OrderBy(x => x)
                .Cast<DateTime?>()
                .FirstOrDefault();

            if (earliest is null)
                return;

            delay = earliest.Value - Now;
        }

        var due = delay.Value;

        if (due < TimeSpan.FromMilliseconds(1))
            due = TimeSpan.FromMilliseconds(1);

        if (due > MAX_TIMER_DELAY)
            due = MAX_TIMER_DELAY;

        _expiryTimer = this.RegisterGrainTimer<object?>(
            static async (self, ct) => await ((PlayerEffectGrain)self!).OnExpiryTimerAsync(ct),
            this,
            new GrainTimerCreationOptions { DueTime = due, Period = Timeout.InfiniteTimeSpan }
        );

        DelayDeactivation(due + DEACTIVATION_MARGIN);
    }
}
