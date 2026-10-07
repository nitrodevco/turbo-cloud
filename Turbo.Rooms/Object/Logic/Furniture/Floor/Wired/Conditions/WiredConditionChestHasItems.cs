using System.Collections.Generic;
using System.Linq;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

/// <summary>
/// Compares what the picked chests hold, together, with a number or a variable: furni in a
/// furni chest, credits in a credit chest. Reads the counts the room already keeps, so it
/// waits on nothing.
/// <para>
/// Int params, as the client's editor writes them: the number, the value-or-variable switch,
/// the variable's target, and the comparison. Furni slot <see cref="ChestSlot"/> is the
/// chests; the variable is read on the next furni slot and user slot 0.
/// </para>
/// </summary>
[RoomObjectLogic("wf_cnd_chest_has_items")]
public class WiredConditionChestHasItems(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int PARAM_VALUE = 0;
    private const int PARAM_VALUE_IS_VARIABLE = 1;
    private const int PARAM_VALUE_TARGET = 2;
    private const int PARAM_COMPARISON = 3;
    private const int VARIABLE_VALUE = 0;
    private const int USER_SLOT_REFERENCE = 0;

    /// <summary>The most the editor lets the number be.</summary>
    private const int MAX_VALUE = 1_000_000;

    public override int WiredCode => (int)WiredConditionType.CHEST_HAS_ITEMS;

    /// <summary>The furni slot of the chests; the variable reference is the slot after it.</summary>
    protected virtual int ChestSlot => 0;

    protected override bool HasPositionalVariableIds => true;

    public override int GetMaxVariableIds() => 1;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredRangeParamRule(0, MAX_VALUE, 0),
            new WiredBoolParamRule(false),
            WiredRules.VariableTarget(WiredVariableTargetType.Furni),
            new WiredEnumParamRule<WiredComparisonType>(WiredComparisonType.GreaterThan),
        ];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [.. Enumerable.Repeat(WiredSources.Furni, ChestSlot + 2)];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        AllVariablesContext();

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var chests = GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, ChestSlot))
            .Select(x => x.Logic)
            .OfType<FurnitureWiredChestLogic>()
            .ToList();

        if (chests.Count == 0)
            return false;

        long reference = GetIntParamOrDefault(PARAM_VALUE, 0);

        if (GetIntParamOrDefault(PARAM_VALUE_IS_VARIABLE, false))
        {
            var selection = new WiredSelectionSet();

            selection.UnionWith(WiredSlotSelection.ForSlot(this, ctx, ChestSlot + 1));
            selection.UnionWith(WiredSlotSelection.ForUserSlot(this, ctx, USER_SLOT_REFERENCE));

            if (
                !TryReadVariableOperand(
                    VARIABLE_VALUE,
                    PARAM_VALUE_TARGET,
                    selection,
                    out reference
                )
            )
                return false;
        }

        return WiredComparison.Compare(
            GetIntParamOrDefault(PARAM_COMPARISON, WiredComparisonType.GreaterThan),
            chests.Sum(x => (long)Count(ctx, x)),
            reference
        );
    }

    /// <summary>What one chest counts as holding.</summary>
    protected virtual int Count(IWiredProcessingContext ctx, FurnitureWiredChestLogic chest) =>
        chest.Kind == WiredChestKind.Coins ? chest.Summary.Coins : chest.Summary.ItemCount;
}
