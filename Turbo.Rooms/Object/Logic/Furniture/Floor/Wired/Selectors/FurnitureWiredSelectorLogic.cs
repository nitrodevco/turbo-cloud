using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;

public abstract class FurnitureWiredSelectorLogic(
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx)
{
    public override WiredType WiredType => WiredType.Selector;

    public override List<Type> GetDefinitionSpecificTypes() =>
        [.. base.GetDefinitionSpecificTypes(), typeof(bool), typeof(bool)];

    public bool GetIsFilter()
    {
        var isFilter = false;

        try
        {
            if (WiredData.DefinitionSpecifics is not null)
            {
                isFilter = (bool)WiredData.DefinitionSpecifics[0]!;
            }
        }
        catch { }

        return isFilter;
    }

    public bool GetIsInvert()
    {
        var isInvert = false;

        try
        {
            if (WiredData.DefinitionSpecifics is not null)
            {
                isInvert = (bool)WiredData.DefinitionSpecifics[1]!;
            }
        }
        catch { }

        return isInvert;
    }

    public virtual Task<IWiredSelectionSet> SelectAsync(
        WiredProcessingContext ctx,
        CancellationToken ct
    ) => Task.FromResult<IWiredSelectionSet>(new WiredSelectionSet());
}
