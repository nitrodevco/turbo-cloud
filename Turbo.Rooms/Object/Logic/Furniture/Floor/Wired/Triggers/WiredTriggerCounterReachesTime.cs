using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

/// <summary>
/// Fires when one of the picked counter clocks reaches the configured time. Params: whole
/// seconds, minutes and the half-second remainder, as the client splits its sliders.
/// </summary>
[RoomObjectLogic("wf_trg_clock_counter")]
public class WiredTriggerCounterReachesTime(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredTriggerLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredTriggerType.CLOCK_REACH_TIME;
    public override List<Type> SupportedEventTypes { get; } = [typeof(WiredClockTickEvent)];

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredRangeParamRule(0, 59, 0), // seconds
            new WiredRangeParamRule(0, 99, 0), // minutes
            new WiredRangeParamRule(0, 1, 0), // half second
        ];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [WiredSources.PickedFurni];

    public int GetTargetHalfSeconds() =>
        (GetIntParamOrDefault(1, 0) * 60 + GetIntParamOrDefault(0, 0)) * 2
        + GetIntParamOrDefault(2, 0);

    public override Task<bool> MatchesEventAsync(RoomEvent evt, CancellationToken ct) =>
        Task.FromResult(
            evt is WiredClockTickEvent tick
                && tick.HalfSeconds == GetTargetHalfSeconds()
                && GetStuffIds().Contains(tick.ObjectId)
        );

    public override Task<bool> CanTriggerAsync(IWiredProcessingContext ctx, CancellationToken ct) =>
        Task.FromResult(ctx.Event is WiredClockTickEvent);
}
