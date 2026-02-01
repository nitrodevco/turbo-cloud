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

        try
        {
            _isFilter = _wiredData.GetDefinitionParam<bool>(0);
            _isInvert = _wiredData.GetDefinitionParam<bool>(1);
        }
        catch { }
    }

    public bool GetIsFilter() => _isFilter;

    public bool GetIsInvert() => _isInvert;

    public virtual Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    ) => Task.FromResult<IWiredSelectionSet>(new WiredSelectionSet());
}
