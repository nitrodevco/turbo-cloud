using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

public abstract class FurnitureWiredAddonLogic(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredLogic(grainFactory, stuffDataFactory, ctx), IWiredAddon
{
    public override WiredType WiredType => WiredType.Addon;

    /// <summary>
    /// The variable box sharing this tile, if any. Addons that extend a variable (sub-variables,
    /// fx) belong to the box they stand on.
    /// </summary>
    protected FurnitureWiredVariableLogic? GetVariableBoxOnTile() =>
        GetLogicOnTile<FurnitureWiredVariableLogic>();

    /// <summary>The first furni of this kind sharing the addon's tile, if any.</summary>
    protected TLogic? GetLogicOnTile<TLogic>()
        where TLogic : class
    {
        foreach (var item in _roomGrain.FurniModule.GetFloorItemsOnTile(_ctx.GetTileIdx()))
        {
            if (item.Logic is TLogic logic)
                return logic;
        }

        return null;
    }

    public virtual Task<bool> MutatePolicyAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    ) => Task.FromResult(true);

    public virtual Task BeforeEffectsAsync(IWiredProcessingContext ctx, CancellationToken ct) =>
        Task.CompletedTask;

    public virtual Task AfterEffectsAsync(IWiredProcessingContext ctx, CancellationToken ct) =>
        Task.CompletedTask;
}
