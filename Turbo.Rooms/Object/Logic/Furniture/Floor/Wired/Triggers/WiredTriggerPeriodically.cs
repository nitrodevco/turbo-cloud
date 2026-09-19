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

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            PeriodicType switch
            {
                WiredPeriodicTriggerType.Short => new WiredRangeParamRule(1, 10, 1),
                _ => new WiredRangeParamRule(1, 120, 1),
            },
        ];

    public int GetPeriodicDelayMs()
    {
        return PeriodicType switch
        {
            WiredPeriodicTriggerType.Short => Math.Clamp(_delayValue, 1, 10) * 50,
            WiredPeriodicTriggerType.Long => Math.Clamp(_delayValue, 1, 120) * 5000,
            _ => Math.Clamp(_delayValue, 1, 120) * WiredPulses.MS,
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
