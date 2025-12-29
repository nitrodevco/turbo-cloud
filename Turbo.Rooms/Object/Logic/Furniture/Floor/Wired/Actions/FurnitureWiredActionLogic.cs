using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

public abstract class FurnitureWiredActionLogic(
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx), IWiredAction
{
    public override WiredType WiredType => WiredType.Action;

    private int _delayInPulses = 0;

    public override List<object> GetDefinitionSpecifics() =>
        [.. base.GetDefinitionSpecifics(), _delayInPulses];

    public abstract Task<bool> ExecuteAsync(IWiredContext ctx, CancellationToken ct);
}
