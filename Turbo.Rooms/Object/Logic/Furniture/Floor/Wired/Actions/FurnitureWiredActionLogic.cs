using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

public abstract class FurnitureWiredActionLogic(
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx)
{
    public override WiredType WiredType => WiredType.Action;

    public override List<Type> GetDefinitionSpecificTypes() =>
        [.. base.GetDefinitionSpecificTypes(), typeof(int)];

    public int GetDelayMs()
    {
        var delay = 0;

        try
        {
            if (WiredData.DefinitionSpecifics is not null)
            {
                delay = (int)WiredData.DefinitionSpecifics[0]!;
            }

            delay = Math.Clamp(delay, 0, 20);
        }
        catch { }

        return delay * 500;
    }

    public virtual Task<bool> ExecuteAsync(WiredExecutionContext ctx, CancellationToken ct) =>
        Task.FromResult(true);
}
