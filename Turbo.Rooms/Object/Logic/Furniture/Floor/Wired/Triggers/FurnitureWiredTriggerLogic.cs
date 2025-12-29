using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

public abstract class FurnitureWiredTriggerLogic(
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx), IWiredTrigger
{
    public override WiredType WiredType => WiredType.Trigger;

    public virtual List<Type> SupportedEventTypes { get; } = [];

    public virtual Task<bool> MatchesEventAsync(RoomEvent evt, CancellationToken ct) =>
        Task.FromResult(SupportedEventTypes.Contains(evt.GetType()));

    public virtual Task<bool> CanTriggerAsync(IWiredContext ctx, CancellationToken ct) =>
        Task.FromResult(false);
}
