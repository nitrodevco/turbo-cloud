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

    /// <summary>
    /// An add-on flashes with its stack's conditions each time the stack passes, and not when
    /// they fail, whether or not an effect then runs (official client capture, golden replay
    /// fixture of the W1-W8 labs 2026-10-24/25: Username 267/267 and Variable Placeholder 76/76
    /// with the stack passing, 0/5 failing; Furni Name 150/150, Execute In Order 21/21, Movement
    /// Physics 12/12, Animation Time 12/12, the filters 56/56, Chest Item Type Scanner 23/23,
    /// Random 10/10, two of them on a stack without an effect, wired matrix addon.1.R3 and
    /// addon.18.R3). The few that never flash say so.
    /// </summary>
    public virtual bool FlashesWithStack => true;

    public virtual Task<bool> MutatePolicyAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    ) => Task.FromResult(true);

    /// <summary>
    /// Records that this kind of add-on changed the firing. False when a lower box of the same
    /// kind already did: Habbo combines two of a kind instead of letting the upper replace the
    /// lower (official client capture 2026-10-24, W1 lab: two Animation Times add up, addon.9.R2
    /// 175 + 2000 = 2175; two Movement Physics add up, addon.7.R2/R3 7/7; two Projectiles keep
    /// the lower box, addon.21.R2).
    /// </summary>
    protected bool IsFirstOfKind(IWiredProcessingContext ctx) =>
        ctx.Policy.AppliedAddons.Add(WiredCode);

    public virtual Task BeforeEffectsAsync(IWiredProcessingContext ctx, CancellationToken ct) =>
        Task.CompletedTask;

    public virtual Task AfterEffectsAsync(IWiredProcessingContext ctx, CancellationToken ct) =>
        Task.CompletedTask;
}
