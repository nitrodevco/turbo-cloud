using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// Turns the variable box on its tile into an experience counter and derives level values
/// from it. Params: which sub-variables to expose (bitmask), the mode (manual table, linear
/// or exponential), then the mode figures: linear step and max level, or exponential first
/// level XP, growth factor and max level. Manual mode reads "level=xp" lines from the string
/// param.
/// </summary>
[RoomObjectLogic("wf_xtra_var_lvlup_system")]
public class WiredAddonVariableLevelUp(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSubVariableAddonLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int MODE_MANUAL = 0;
    private const int MODE_LINEAR = 1;
    private const int MODE_EXPONENTIAL = 2;

    // Bounds of the level count slider in the client editor.
    private const int MIN_LEVELS = 2;
    private const int MAX_LEVELS = 100000;

    private static readonly string[] SUB_NAMES =
    [
        "current_level",
        "current_xp",
        "progress",
        "progress_percentage",
        "xp_required",
        "xp_remaining",
        "is_maxed",
        "max_level",
    ];

    private List<long> _thresholds = [];

    public override int WiredCode => (int)WiredAddonType.VARIABLE_LEVEL_UP;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredRangeParamRule(0, 255, 255), new WiredRangeParamRule(0, 2, 1)];

    public override IWiredParamRule? GetIntParamTailRule() => WiredRules.NonNegative();

    protected override async Task FillInternalDataAsync(CancellationToken ct)
    {
        await base.FillInternalDataAsync(ct);

        _thresholds = BuildThresholds();
    }

    protected override IEnumerable<IWiredVariable> BuildSubVariables(
        FurnitureWiredVariableLogic parent,
        string parentName
    )
    {
        var mask = GetIntParamOrDefault(0, 255);

        for (var i = 0; i < SUB_NAMES.Length; i++)
        {
            if ((mask & (1 << i)) == 0)
                continue;

            var index = i;

            yield return Create(
                index,
                parentName,
                SUB_NAMES[index],
                parent,
                (p, key) => Compute(p, key, index)
            );
        }
    }

    private WiredVariableValue? Compute(IWiredVariable parent, WiredVariableKey key, int index)
    {
        if (!parent.TryGetValue(key, out var xpValue) || _thresholds.Count == 0)
            return null;

        long xp = Math.Max(0, xpValue.Value);
        var maxLevel = _thresholds.Count;
        var level = 1;

        while (level < maxLevel && xp >= _thresholds[level])
            level++;

        var levelStart = _thresholds[level - 1];
        var nextStart = level < maxLevel ? _thresholds[level] : levelStart;
        var required = Math.Max(0, nextStart - levelStart);
        var progress = Math.Max(0, xp - levelStart);
        var isMaxed = level >= maxLevel;

        long result = index switch
        {
            0 => level,
            1 => xp,
            2 => isMaxed ? required : Math.Min(progress, required),
            3 => isMaxed || required == 0 ? 100 : Math.Min(100, progress * 100 / required),
            4 => required,
            5 => isMaxed ? 0 : Math.Max(0, required - progress),
            6 => isMaxed ? 1 : 0,
            7 => maxLevel,
            _ => 0,
        };

        return new WiredVariableValue((int)Math.Clamp(result, int.MinValue, int.MaxValue));
    }

    /// <summary>XP needed to reach each level, index 0 being level 1 (always zero).</summary>
    private List<long> BuildThresholds()
    {
        var mode = GetIntParamOrDefault(1, MODE_LINEAR);
        var thresholds = new List<long> { 0 };

        switch (mode)
        {
            case MODE_LINEAR:
            {
                var step = Math.Max(1, GetIntParamOrDefault(2, 100));
                var maxLevel = Math.Clamp(GetIntParamOrDefault(3, 50), MIN_LEVELS, MAX_LEVELS);

                for (var level = 2; level <= maxLevel; level++)
                    thresholds.Add((long)(level - 1) * step);

                break;
            }
            case MODE_EXPONENTIAL:
            {
                var first = Math.Max(1, GetIntParamOrDefault(2, 100));
                var factorPercent = Math.Max(0, GetIntParamOrDefault(3, 20));
                var maxLevel = Math.Clamp(GetIntParamOrDefault(4, 50), MIN_LEVELS, MAX_LEVELS);
                double required = first;
                long total = 0;

                for (var level = 2; level <= maxLevel; level++)
                {
                    total += (long)required;
                    thresholds.Add(total);
                    required *= 1 + factorPercent / 100.0;

                    if (total > int.MaxValue)
                        break;
                }

                break;
            }
            default:
            {
                var lines = (_wiredData.StringParam ?? string.Empty).Split(
                    ['\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries
                );
                var table = new SortedDictionary<int, long>();

                foreach (var line in lines)
                {
                    var parts = line.Split('=');

                    if (
                        parts.Length == 2
                        && int.TryParse(parts[0].Trim(), out var level)
                        && long.TryParse(parts[1].Trim(), out var xp)
                        && level >= 2
                    )
                        table[level] = xp;
                }

                thresholds.AddRange(table.Values.OrderBy(x => x));

                break;
            }
        }

        return thresholds;
    }
}
