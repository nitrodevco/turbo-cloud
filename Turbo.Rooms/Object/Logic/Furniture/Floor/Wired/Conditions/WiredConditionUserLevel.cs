using System.Collections.Generic;
using System.Linq;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;
using Turbo.Rooms.Wired.Variables;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

/// <summary>
/// True when the selected users' avatar level compares to the set level (Flash
/// <c>ConditionCodes.USER_LEVEL</c> 44, editor <c>conditions.LevelMatches</c>): a level slider
/// from 1 to 30 ("Set level: %level%") and the comparison radio (0 less than, 1 equal,
/// 2 greater than), written as [level, comparison].
/// <para>
/// The level is the room's <c>@level</c> user variable (id -210), the avatar level Habbo's
/// variable picker lists; that the condition reads the same value is inferred from the two
/// sharing the level and the box not being in Habbo's furnidata yet. A room without
/// <c>@level</c> has no level to compare, so the condition is not met.
/// </para>
/// The classname is this server's (no official furni exists yet); a hotel may also point a
/// definition's logic column at it.
/// </summary>
[RoomObjectLogic("wf_cnd_user_level")]
public class WiredConditionUserLevel(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    public const string LEVEL_VARIABLE = "@level";

    // The editor's slider (createSliderSection(..., 1, 30, 1), starting at 1).
    private const int MIN_LEVEL = 1;
    private const int MAX_LEVEL = 30;

    private const int COMPARISON_EQUALS = 1;

    public override int WiredCode => (int)WiredConditionType.USER_LEVEL;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredRangeParamRule(MIN_LEVEL, MAX_LEVEL, MIN_LEVEL),
            new WiredRangeParamRule(0, 2, COMPARISON_EQUALS),
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var level = GetIntParamOrDefault(0, MIN_LEVEL);
        var comparison = GetIntParamOrDefault(1, COMPARISON_EQUALS);

        if (
            !HabboVariableIds.TryGet(WiredVariableTargetType.User, LEVEL_VARIABLE, out var id)
            || WiredSystem.GetVariableById(id) is not { } variable
        )
            return false;

        return Quantify(
            GetTargetIds(WiredVariableTargetType.User, ctx.GetSelection(this))
                .Select(targetId =>
                    ReadVariable(variable, WiredVariableTargetType.User, targetId) is { } value
                    && WiredComparison.CompareThreeWay(comparison, value, level)
                ),
            true
        );
    }
}
