using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
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

    public bool GetIsInvert() => _isInvert;

    public virtual bool SelectsFurni => true;

    public virtual bool SelectsUsers => false;

    public virtual Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    ) => Task.FromResult<IWiredSelectionSet>(new WiredSelectionSet());
}
