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
) : FurnitureWiredLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx), IWiredSelector
{
    public override WiredType WiredType => WiredType.Selector;

    private bool _isFiler;
    private bool _isInvert;

    public override List<object> GetDefinitionSpecifics() =>
        [.. base.GetDefinitionSpecifics(), _isFiler, _isInvert];

    public virtual Task<IWiredSelectionSet> SelectAsync(IWiredContext ctx, CancellationToken ct) =>
        Task.FromResult<IWiredSelectionSet>(new WiredSelectionSet());
}
