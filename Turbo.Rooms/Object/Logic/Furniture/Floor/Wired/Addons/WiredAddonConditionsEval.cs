using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// Changes how the conditions of the stack combine.
///
/// Int params, as the client's editor writes them: the plain mode (all, at least one, not all,
/// none), or <see cref="COUNTED"/> when one of the three counted modes was picked; then which
/// counted mode (less than, exactly, more than) and the number it compares against. The editor
/// keeps one number input per counted mode, so the two later params are zero unless a counted
/// mode is chosen.
///
/// The furni slot holds the conditions the mode applies to ("You can select the WIRED
/// Conditions this add-on applies to. If the selection is empty, it will apply to all available
/// conditions."); only conditions and add-ons can be picked. Habbo clamps the mode into range
/// (9 is stored as 3) and stores the counted mode as sent (official client capture
/// 2026-10-22T22-57-02-UpdateAddon), a negative one as 0, and the number as sent, past the
/// editor's bounds too (-1000, 1001, 99999: official client capture WIN63-202610201032,
/// 2026-10-23T00-04-56 / 00-06-20-UpdateAddon).
/// </summary>
[RoomObjectLogic("wf_xtra_or_eval")]
public class WiredAddonConditionsEval(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int PARAM_MODE = 0;
    private const int PARAM_COUNTED_MODE = 1;
    private const int PARAM_COUNT = 2;

    /// <summary>What the mode param holds when the player picked one of the counted modes.</summary>
    private const int COUNTED = -1;

    private const int MODE_ALL = 0;
    private const int MODE_ANY = 1;
    private const int MODE_NOT_ALL = 2;
    private const int MODE_NONE = 3;

    private const int COUNTED_LESS_THAN = 0;
    private const int COUNTED_EXACTLY = 1;
    private const int COUNTED_MORE_THAN = 2;

    // Counted modes the editor does not offer, which Habbo runs all the same (official client
    // capture WIN63-202610201032, 2026-10-23T00-06-44 / 00-07-24-UpdateAddon); 6 never passed, and
    // anything higher is taken to do the same (inference: 7 and up were not tried).
    private const int COUNTED_AT_MOST = 3;
    private const int COUNTED_NOT_EXACTLY = 4;
    private const int COUNTED_AT_LEAST = 5;

    public override int WiredCode => (int)WiredAddonType.CONDITION_EVALUATION;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            // A fresh box asks for at least one on the official server (defaults [1,0,0],
            // 2026-10-20T17-35-39-Open-wait).
            new WiredClampParamRule(COUNTED, MODE_NONE, MODE_ANY),
            new WiredClampParamRule(COUNTED_LESS_THAN, int.MaxValue, COUNTED_LESS_THAN),
            new WiredParamRule(0),
        ];

    // "Select Furni [0/20]", picked furni by default (official client capture
    // 2026-10-22T22-55-58-Open: allowed [0,100,200,201], default 100).
    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    protected override bool CanPickFurni(IRoomItem item) =>
        item.Logic is IWiredCondition or IWiredAddon;

    protected override string? PickRefusedErrorKey => WiredSaveErrors.CONDITION_EVALUATION_FURNI;

    public override Task<bool> MutatePolicyAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        var mode = GetIntParamOrDefault(PARAM_MODE, MODE_ANY);

        ctx.Policy.ConditionMode = mode switch
        {
            MODE_ANY => WiredConditionModeType.Any,
            MODE_NOT_ALL => WiredConditionModeType.NotAll,
            MODE_NONE => WiredConditionModeType.NoneMatch,
            COUNTED => GetIntParamOrDefault(PARAM_COUNTED_MODE, COUNTED_LESS_THAN) switch
            {
                <= COUNTED_LESS_THAN => WiredConditionModeType.LessThan,
                COUNTED_EXACTLY => WiredConditionModeType.Exactly,
                COUNTED_MORE_THAN => WiredConditionModeType.MoreThan,
                COUNTED_AT_MOST => WiredConditionModeType.AtMost,
                COUNTED_NOT_EXACTLY => WiredConditionModeType.NotExactly,
                COUNTED_AT_LEAST => WiredConditionModeType.AtLeast,
                _ => WiredConditionModeType.Never,
            },
            _ => WiredConditionModeType.All,
        };
        ctx.Policy.ConditionThreshold = GetIntParamOrDefault(PARAM_COUNT, 0);

        var picked = WiredSlotSelection.ForSlot(this, ctx, 0).SelectedFurniIds;

        ctx.Policy.ConditionIds = picked.Count > 0 ? picked : null;

        return Task.FromResult(true);
    }
}
