using System.Collections.Generic;
using System.Linq;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture;
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
/// Compares what each picked chest holds with a number or a variable: furni in a furni chest,
/// credits in a credit chest. The quantifier folds the chests: all of them, or any one. Reads the
/// counts the room already keeps, so it waits on nothing.
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

    // Both chest conditions open with the furni quantifier (official client capture 2026-10-24,
    // quantifierType 1 on 6 of 6 opens).
    protected override WiredQuantifierType QuantifierType => WiredQuantifierType.Furni;

    protected override bool AllowsWallFurni => true;

    /// <summary>The furni slot of the chests; the variable reference is the slot after it.</summary>
    protected virtual int ChestSlot => 0;

    protected override bool HasPositionalVariableIds => true;

    public override int GetMaxVariableIds() => 1;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredRangeParamRule(0, MAX_VALUE, 0),
            new WiredBoolParamRule(false),
            // A fresh box reads a context variable on the official server (both chest
            // conditions: defaults [0,0,-20,2], 2026-10-20T17-37-19-Open-wait).
            WiredRules.VariableTarget(WiredVariableTargetType.Context),
            new WiredEnumParamRule<WiredComparisonType>(WiredComparisonType.GreaterThan),
        ];

    // The chests default to the picked furni and the variable reference to the selectors
    // (official client capture).
    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [WiredSources.Furni, WiredSources.Furni.DefaultingTo(WiredFurniSourceType.SelectorItems)];

    // The chests slot takes chests of either kind (wiredfurni.error.require_chests; which box
    // the text is for is inferred from its wording).
    protected override string? RefusePickInSlot(int slot, IRoomItem item) =>
        slot != ChestSlot || item.Logic is FurnitureWiredChestLogic
            ? null
            : WiredSaveErrors.REQUIRE_CHESTS;

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [WiredSources.Users.DefaultingTo(WiredPlayerSourceType.SelectorUsers)];

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        AllVariablesContext();

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var chests = GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, ChestSlot))
            .Select(x => x.Logic)
            .OfType<FurnitureWiredChestLogic>()
            .ToList();

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

        var comparison = GetIntParamOrDefault(PARAM_COMPARISON, WiredComparisonType.GreaterThan);

        return Quantify(
            Counts(ctx, chests)
                .Select(count => WiredComparison.Compare(comparison, count, reference)),
            QuantifierRequiresAll
        );
    }

    /// <summary>
    /// The counts the quantifier folds: one per chest. With no chest picked "all" holds and "any"
    /// does not, whatever the comparison, while a picked empty chest counts 0 (official client
    /// capture 2026-10-24, wired matrix condition.45.R1 8 of 8, R1b 8 of 8). That each chest is
    /// compared alone, not their sum, is inferred from the quantifier: one chest was recorded.
    /// </summary>
    protected virtual IEnumerable<long> Counts(
        IWiredProcessingContext ctx,
        List<FurnitureWiredChestLogic> chests
    ) =>
        chests.Select(chest =>
            (long)(
                chest.Kind == WiredChestKind.Coins ? chest.Summary.Coins : chest.Summary.ItemCount
            )
        );
}
