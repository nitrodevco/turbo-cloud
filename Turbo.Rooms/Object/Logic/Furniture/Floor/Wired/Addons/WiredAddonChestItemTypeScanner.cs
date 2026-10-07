using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
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

    public override int GetMaxVariableIds() => 1;

    public override List<IWiredParamRule> GetIntParamRules() => [new WiredRangeParamRule(0, 1, 0)];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [WiredSources.Furni, WiredSources.Furni];

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
        var amount = Count(ctx);

        if (variable.TryGetValue(key, out _))
            await variable.SetValueAsync(new WiredExecutionContext(_roomGrain), key, amount);
        else
            await variable.GiveValueAsync(key, amount, true);
    }

    /// <summary>The items of the picked types in the picked chests, as the scanning option counts them.</summary>
    internal int Count(IWiredProcessingContext ctx)
    {
        var types = GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, SLOT_ITEM_TYPES))
            .Select(x => ChestItemTypes.Of(x.Definition, x.Logic.StuffData.GetSnapshot()))
            .ToHashSet();
        var chests = GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, SLOT_CHESTS))
            .Select(x => x.Logic)
            .OfType<FurnitureWiredChestLogic>()
            .Where(x => x.Kind == WiredChestKind.Furni);

        if (types.Count == 0)
            return 0;

        if (GetIntParamOrDefault(0, 0) == MODE_PREVIEWED_ONLY)
            return chests.Sum(chest => chest.Summary.Preview.Count(types.Contains));

        return chests.Sum(chest =>
            types.Sum(type => chest.Summary.CountsByType.TryGetValue(type, out var n) ? n : 0)
        );
    }
}
