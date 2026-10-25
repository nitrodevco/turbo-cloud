using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

/// <summary>
/// Fires on its own every N units. The wired system paces it from the room tick; the event it
/// receives carries no actor. The plain box counts in half-second pulses, the short one in
/// 50 ms steps and the long one in five-second steps.
/// </summary>
[RoomObjectLogic("wf_trg_periodically")]
public class WiredTriggerPeriodically(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredTriggerLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredTriggerType.TRIGGER_PERIODICALLY;
    public override List<Type> SupportedEventTypes { get; } = [typeof(PeriodicRoomEvent)];

    public virtual WiredPeriodicTriggerType PeriodicType => WiredPeriodicTriggerType.Normal;

    private int _delayValue = 1;

    // As many units as fit in the int of milliseconds the delay is counted in.
    private const int MAX_LONG_UNITS = int.MaxValue / 5000;
    private const int MAX_PULSES = int.MaxValue / WiredPulses.MS;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            // A fresh box ticks every 0.5 s, the short one every 200 ms (4) and the long one
            // every 50 s (10) on the official server (2026-10-20T17-31-49-Open-wait).
            // Out of the editor's range, the short one's 11 was stored as 10 and its 0 as 1;
            // the plain and long ones store 121 as 121 and 0 as 1, and the plain one at 121 ran
            // every 60.5 s (official client capture 2026-10-24/25, trigger.19.E10 /
            // trigger.12.E10, trigger.6.B2 / trigger.12.B2 / trigger.19.B2).
            PeriodicType switch
            {
                WiredPeriodicTriggerType.Short => new WiredClampParamRule(1, 10, 4),
                WiredPeriodicTriggerType.Long => new WiredClampParamRule(1, MAX_LONG_UNITS, 10),
                _ => new WiredClampParamRule(1, MAX_PULSES, 1),
            },
        ];

    public int GetPeriodicDelayMs()
    {
        return PeriodicType switch
        {
            WiredPeriodicTriggerType.Short => Math.Clamp(_delayValue, 1, 10) * 50,
            WiredPeriodicTriggerType.Long => Math.Clamp(_delayValue, 1, MAX_LONG_UNITS) * 5000,
            _ => Math.Clamp(_delayValue, 1, MAX_PULSES) * WiredPulses.MS,
        };
    }

    public override Task<bool> CanTriggerAsync(IWiredProcessingContext ctx, CancellationToken ct) =>
        Task.FromResult(ctx.Event is PeriodicRoomEvent);

    protected override async Task FillInternalDataAsync(CancellationToken ct)
    {
        await base.FillInternalDataAsync(ct);

        _delayValue = GetIntParamOrDefault(0, 1);
    }
}
