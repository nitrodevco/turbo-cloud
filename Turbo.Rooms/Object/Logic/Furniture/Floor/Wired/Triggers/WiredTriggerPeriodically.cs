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

[RoomObjectLogic("wf_trg_periodically")]
public class WiredTriggerPeriodically(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredTriggerLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredTriggerType.TRIGGER_PERIODICALLY;
    public override List<Type> SupportedEventTypes { get; } = [typeof(PeriodicRoomEvent)];

    public virtual WiredPeriodicTriggerType PeriodicType => WiredPeriodicTriggerType.Short;

    private int _delayValue = 0;

    public override List<IWiredParamRule> GetIntParamRules() => [new WiredRangeParamRule(1, 10, 1)];

    public int GetPeriodicDelayMs()
    {
        return PeriodicType switch
        {
            WiredPeriodicTriggerType.Short => Math.Clamp(_delayValue, 1, 10) * 50,
            WiredPeriodicTriggerType.Long => Math.Clamp(_delayValue, 1, 120) * 5000,
            _ => 50,
        };
    }

    protected override async Task FillInternalDataAsync(CancellationToken ct)
    {
        await base.FillInternalDataAsync(ct);

        try
        {
            //_delayValue = WiredData.GetIntParam<int>(0); WiredData.IntParams?[0] ?? 0;
        }
        catch { }
    }
}
