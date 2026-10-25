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
/// Fires once when the room wired timer reaches the configured time (in half-second pulses)
/// and again after every "reset timers". Paced by the wired system, not by an event.
/// </summary>
[RoomObjectLogic("wf_trg_at_given_time")]
public class WiredTriggerAtTime(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredTriggerLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredTriggerType.TRIGGER_ONCE;
    public override List<Type> SupportedEventTypes { get; } = [typeof(PeriodicRoomEvent)];

    // Stored as sent past the editor's 1..1200: 0 stayed 0 and fired on the first tick after a
    // Timer Reset, 1201 stayed 1201 (official client capture 2026-10-25, wired matrix
    // trigger.3.B2, 2/2). A negative time is pulled up to 0 (inference, not recorded).
    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredClampParamRule(0, int.MaxValue, 1)];

    public long GetTargetMs() => WiredPulses.ToMs(Math.Max(0, GetIntParamOrDefault(0, 1)));

    public override Task<bool> CanTriggerAsync(IWiredProcessingContext ctx, CancellationToken ct) =>
        Task.FromResult(ctx.Event is PeriodicRoomEvent);
}
