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

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

[RoomObjectLogic("wf_trg_periodically")]
public class WiredTriggerPeriodically(
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredTriggerLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredTriggerType.TRIGGER_PERIODICALLY;
    public override List<Type> SupportedEventTypes { get; } = [typeof(PeriodicRoomEvent)];

    public virtual WiredPeriodicTriggerType PeriodicType => WiredPeriodicTriggerType.Short;

    private int _delayValue = 0;

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
        try
        {
            _delayValue = WiredData.IntParams?[0] ?? 0;
        }
        catch { }

        await base.FillInternalDataAsync(ct);
    }
}
