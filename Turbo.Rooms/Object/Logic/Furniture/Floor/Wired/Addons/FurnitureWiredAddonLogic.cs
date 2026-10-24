using System.Linq;
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

    /// <summary>
    /// The first furni of this kind sharing the addon's tile, else the first of the add-ons a
    /// "From Another Room" on the tile imports, if any.
    /// </summary>
    protected TLogic? GetLogicOnTile<TLogic>()
        where TLogic : class
    {
        WiredVariableReference? reference = null;

        foreach (var item in FurniModule.GetFloorItemsOnTile(_ctx.GetTileIdx()))
        {
            if (item.Logic is TLogic logic)
                return logic;

            reference ??= item.Logic as WiredVariableReference;
        }

        return reference?.ImportedAddons.OfType<TLogic>().FirstOrDefault();
    }

    public virtual Task<bool> MutatePolicyAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    ) => Task.FromResult(true);

    /// <summary>
    /// Records that this kind of add-on changed the firing. False when a lower box of the same
    /// kind already did and the hotel keeps Habbo's rule for two of a kind
    /// (<see cref="Configuration.WiredConfig.DoubledAddonsAsMeasured"/>); with it off every box
    /// counts as the first, so the upper one replaces the lower.
    /// </summary>
    protected bool IsFirstOfKind(IWiredProcessingContext ctx) =>
        ctx.Policy.AppliedAddons.Add(WiredCode) || !WiredConfig.DoubledAddonsAsMeasured;

    public virtual Task BeforeEffectsAsync(IWiredProcessingContext ctx, CancellationToken ct) =>
        Task.CompletedTask;

    public virtual Task AfterEffectsAsync(IWiredProcessingContext ctx, CancellationToken ct) =>
        Task.CompletedTask;
}
