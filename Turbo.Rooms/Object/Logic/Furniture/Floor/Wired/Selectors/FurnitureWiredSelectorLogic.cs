using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;

public abstract class FurnitureWiredSelectorLogic(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredLogic(grainFactory, stuffDataFactory, ctx), IWiredSelector
{
    public override WiredType WiredType => WiredType.Selector;

    private bool _isFilter = false;
    private bool _isInvert = false;

    public override List<Type> GetDefinitionSpecificTypes() =>
        [.. base.GetDefinitionSpecificTypes(), typeof(bool), typeof(bool)];

    protected override async Task FillInternalDataAsync(CancellationToken ct)
    {
        await base.FillInternalDataAsync(ct);

        _isFilter = _wiredData.GetDefinitionParam<bool>(0);
        _isInvert = _wiredData.GetDefinitionParam<bool>(1);
    }

    public bool GetIsFilter() => _isFilter;

    /// <summary>
    /// A selector flashes each time its stack runs, between its resting state + 1 and + 2; it rests
    /// at 0, or 3 in filter mode (recorded on hhus, WIN63-202610121709: an area selector 1/2, filter
    /// "furni by type" and "furni with variable" selectors 4/5).
    /// </summary>
    public override Task FlashActivationStateAsync(CancellationToken ct)
    {
        var resting = _isFilter ? 3 : 0;

        return FlashWithinAsync(resting + 1, resting + 2);
    }

    /// <summary>
    /// A saved selector is put back to its resting state and the room told, the saver first:
    /// Habbo answers an UpdateSelector with ObjectDataUpdate(selector, "0") before
    /// WiredSaveSuccess, and any other box's save with WiredSaveSuccess alone (official client
    /// captures 2026-10-14 09:46 and 2026-10-16 06:10/06:11/07:03, 6 of 6 selector saves, none in
    /// filter mode; a filter rests at 3, see <see cref="FlashActivationStateAsync"/>).
    /// </summary>
    public override async Task<bool> ApplyWiredUpdateAsync(
        ActionContext ctx,
        UpdateWiredMessage update,
        CancellationToken ct
    )
    {
        if (!await base.ApplyWiredUpdateAsync(ctx, update, ct))
            return false;

        await SetStateAsync(_wiredData.GetDefinitionParam<bool>(0) ? 3 : 0, false);

        var composer = _ctx.RoomObject.GetRefreshStuffDataComposer();

        if (ctx.PlayerId <= 0)
        {
            await _roomGrain.SendComposerToRoomAsync(composer, ct);

            return true;
        }

        // Straight to the saver, so the save's answer that follows reaches them after it.
        await _roomGrain.SendComposerToRoomExceptAsync(composer, [ctx.PlayerId], ct);
        await _grainFactory.SendComposerToPlayerAsync(ctx.PlayerId, composer, ct);

        return true;
    }

    public bool GetIsInvert() => _isInvert;

    public virtual bool SelectsFurni => true;

    public virtual bool SelectsUsers => false;

    public virtual Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    ) => Task.FromResult<IWiredSelectionSet>(new WiredSelectionSet());
}
