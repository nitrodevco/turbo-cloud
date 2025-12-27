using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots.WiredData;
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

    public abstract List<Type> SupportedEventTypes { get; }
    public abstract Task<bool> MatchesAsync(IWiredContext ctx);

    public virtual Task<bool> MatchesEventAsync(RoomEvent evt) =>
        Task.FromResult(SupportedEventTypes.Contains(evt.GetType()));

    public virtual async Task<bool> CanTriggerAsync(IWiredContext ctx)
    {
        if (!await MatchesAsync(ctx))
            return false;

        _ = FlashActivationStateAsync();

        return true;
    }

    protected override WiredDataSnapshot BuildSnapshot() =>
        new WiredDataTriggerSnapshot()
        {
            FurniLimit = _furniLimit,
            StuffIds = WiredData.StuffIds,
            StuffTypeId = _ctx.Definition.SpriteId,
            Id = _ctx.ObjectId,
            StringParam = WiredData.StringParam,
            IntParams = WiredData.IntParams,
            VariableIds = WiredData.VariableIds,
            FurniSourceTypes = WiredData.FurniSources,
            UserSourceTypes = WiredData.PlayerSources,
            Code = WiredCode,
            AdvancedMode = true,
            AmountFurniSelections = [],
            AllowWallFurni = false,
            AllowedFurniSources = GetFurniSources(),
            AllowedUserSources = GetPlayerSources(),
        };
}
