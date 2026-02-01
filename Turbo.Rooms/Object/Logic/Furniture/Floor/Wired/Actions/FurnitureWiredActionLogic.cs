using System;
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
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredLogic(grainFactory, stuffDataFactory, ctx), IWiredAction
{
    public override WiredType WiredType => WiredType.Action;

    public override List<Type> GetDefinitionSpecificTypes() =>
        [.. base.GetDefinitionSpecificTypes(), typeof(int)];

    public int GetDelayMs()
    {
        var delay = 0;

        try
        {
            if (_wiredData.DefinitionSpecifics is not null)
            {
                delay = (int)_wiredData.DefinitionSpecifics[0]!;
            }

            delay = Math.Clamp(delay, 0, 20);
        }
        catch { }

        return delay * 500;
    }

    public virtual Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct) =>
        Task.FromResult(true);
}
