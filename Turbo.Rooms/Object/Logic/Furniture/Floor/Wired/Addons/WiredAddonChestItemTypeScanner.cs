using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Primitives.WiredTrading;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// "WIRED Scanner Add-on: Chest Furni of Type": before the effects run, counts the items of the
/// types the furni of slot 0 are inside the chests of slot 1 and stores the amount in the picked
/// context variable (<c>wiredfurni.params.chest_item_type_scanner.info</c>). Param 0 is the
/// scanning option: 0 scans all items in the chests, 1 only the items a chest previews above
/// itself. The client's editor is <c>addons/chests/ChestItemTypeScanner</c>.
/// </summary>
[RoomObjectLogic("wf_xtra_scan_chest_furni_by_type")]
public class WiredAddonChestItemTypeScanner(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int SLOT_ITEM_TYPES = 0;
    private const int SLOT_CHESTS = 1;
    private const int MODE_PREVIEWED_ONLY = 1;

    public override int WiredCode => (int)WiredAddonType.CHEST_ITEM_TYPE_SCANNER;

    protected override bool AllowsWallFurni => true;

    public override int GetMaxVariableIds() => 1;

    public override List<IWiredParamRule> GetIntParamRules() => [new WiredRangeParamRule(0, 1, 0)];

    // The furni types default to the triggering furni and the chests to the picked furni
    // (official client capture).
    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            WiredSources.FurniOrSecondary.DefaultingTo(WiredFurniSourceType.TriggeredItem),
            WiredSources.FurniOrSecondary,
        ];

    // The chests slot takes furni chests (wiredfurni.error.require_furni_chests; which box the
    // text is for is inferred from its wording).
    protected override string? RefusePickInSlot(int slot, IRoomItem item) =>
        slot != SLOT_CHESTS || item.Logic is FurnitureWiredChestLogic { Kind: WiredChestKind.Furni }
            ? null
            : WiredSaveErrors.REQUIRE_FURNI_CHESTS;

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        AllVariablesContext();

    public override async Task BeforeEffectsAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        var variable = GetVariable(0);

        if (variable is null)
            return;

        var snapshot = variable.GetVarSnapshot();

        if (snapshot.TargetType != WiredVariableTargetType.Context)
            return;

        var key = new WiredVariableKey(snapshot.VariableId, snapshot.TargetType, 0);

        if (Count(ctx) is not { } amount)
            return;

        if (variable.TryGetValue(key, out _))
            await variable.SetValueAsync(new WiredExecutionContext(_roomGrain), key, amount);
        else
            await variable.GiveValueAsync(key, amount, true);
    }

    /// <summary>
    /// The items of the picked types in the picked chests, as the scanning option counts them, or
    /// null when the chests hold nothing: the context variable then gets no value, as with no
    /// scanner on the stack (official client capture 2026-10-24, W7 lab addon.18.R1, a Variable
    /// Placeholder printed "SC=null" for an empty chest, 4/4 all items, 1/1 previewed). No chest
    /// picked counts as nothing held (inferred, not measured).
    /// </summary>
    internal int? Count(IWiredProcessingContext ctx)
    {
        var types = GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, SLOT_ITEM_TYPES))
            .Select(x => ChestItemTypes.Of(x.Definition, x.Logic.StuffData.GetSnapshot()))
            .ToHashSet();
        var chests = GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, SLOT_CHESTS))
            .Select(x => x.Logic)
            .OfType<FurnitureWiredChestLogic>()
            .Where(x => x.Kind == WiredChestKind.Furni)
            .ToList();

        if (chests.All(x => x.Summary.ItemCount == 0))
            return null;

        if (types.Count == 0)
            return 0;

        if (GetIntParamOrDefault(0, 0) == MODE_PREVIEWED_ONLY)
            return chests.Sum(chest => chest.Summary.Preview.Count(types.Contains));

        return chests.Sum(chest =>
            types.Sum(type => chest.Summary.CountsByType.TryGetValue(type, out var n) ? n : 0)
        );
    }
}
