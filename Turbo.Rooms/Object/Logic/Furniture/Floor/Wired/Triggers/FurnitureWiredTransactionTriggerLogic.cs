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
/// The two transaction triggers: they fire for the user whose transaction it was, when it was
/// for one of the picked contracts (or Initiate Transaction boxes, for a custom contract), or
/// for any transaction when nothing is picked.
/// </summary>
public abstract class FurnitureWiredTransactionTriggerLogic<TEvent>(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredTriggerLogic(grainFactory, stuffDataFactory, ctx)
    where TEvent : RoomEvent
{
    public override List<Type> SupportedEventTypes { get; } = [typeof(TEvent)];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [WiredSources.PickedFurni];

    protected abstract RoomObjectId SourceOf(TEvent evt);

    public override Task<bool> MatchesEventAsync(RoomEvent evt, CancellationToken ct)
    {
        var picked = GetStuffIds();

        return Task.FromResult(
            evt is TEvent transaction
                && (picked.Count == 0 || picked.Contains(SourceOf(transaction).Value))
        );
    }

    public override Task<bool> CanTriggerAsync(IWiredProcessingContext ctx, CancellationToken ct) =>
        Task.FromResult(ctx.Event is TEvent);
}
