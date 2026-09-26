using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

/// <summary>
/// A trigger that fires when something happens to one of the furni it picked: walked on or
/// off, clicked, used, changed state. The furni is the subject, so a box with none picked
/// never fires. The five boxes were written out in full and differed only in the event.
/// (The "bot reaches furni" trigger is not one of these: its subject is the bot, and picked
/// furni only narrow where it may arrive, so an empty pick there means anywhere.)
/// </summary>
public abstract class FurnitureWiredFurniEventTriggerLogic<TEvent>(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredTriggerLogic(grainFactory, stuffDataFactory, ctx)
    where TEvent : RoomEvent
{
    public override List<Type> SupportedEventTypes { get; } = [typeof(TEvent)];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [WiredSources.PickedFurni];

    /// <summary>The furni the event happened to.</summary>
    protected abstract RoomObjectId GetFurniId(TEvent evt);

    public override Task<bool> CanTriggerAsync(IWiredProcessingContext ctx, CancellationToken ct) =>
        Task.FromResult(
            ctx.Event is TEvent evt
                && ctx.GetSelection(this).SelectedFurniIds.Contains(GetFurniId(evt).Value)
        );
}
