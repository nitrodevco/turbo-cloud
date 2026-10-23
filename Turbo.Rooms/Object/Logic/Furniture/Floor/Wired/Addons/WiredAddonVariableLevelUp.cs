using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// Turns the variable box on its tile into an experience counter and derives level values
/// from it. Params: which sub-variables to expose (bitmask), the mode (manual table, linear
/// or exponential), then the mode figures: linear step and max level, or exponential first
/// level XP, growth factor and max level. Manual mode reads "level=xp" lines from the string
/// param.
///
/// The editor sends only the mode's ints (2 manual, 4 linear, 5 exponential); Habbo stores 5,
/// padding with 0 and zeroing the ones the mode does not use, raises a growth factor below 1
/// and a max level below 2 instead of refusing them, keeps any sub-variable mask, and stores a
/// manual table with a line that is not "level=xp" as empty (official client capture
/// 2026-10-22T22-57-46 / 22-58-38 / 22-59-07).
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

    private const int PARAM_MODE = 1;
    private const int PARAM_LINEAR_MAX_LEVEL = 3;
    private const int PARAM_EXPONENTIAL_FACTOR = 3;
    private const int PARAM_EXPONENTIAL_MAX_LEVEL = 4;
    private const int MIN_FACTOR = 1;

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

    // A fresh box exposes no sub-variables and holds linear figures plus a third one (the editor
    // sends two ints for manual, four for linear, five for exponential: AS3 wired_setup/addons/
    // VariableLvlUpAddon.as:133-151), like the official one (defaults [0,1,100,50,50],
    // 2026-10-20T17-35-39-Open-wait).
    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredParamRule(0), // sub-variable mask
            new WiredRangeParamRule(MODE_MANUAL, MODE_EXPONENTIAL, MODE_LINEAR),
            new WiredRangeParamRule(0, int.MaxValue, 100), // step size / first level XP
            new WiredRangeParamRule(0, int.MaxValue, 50), // max level / growth factor
            new WiredRangeParamRule(0, int.MaxValue, 50), // exponential max level
        ];

    protected override int GetRequiredIntParamCount() => PARAM_MODE + 1;

    protected override bool TryNormalizeIntParams(List<int> proposed, out List<int> normalized)
    {
        if (!base.TryNormalizeIntParams(proposed, out normalized))
            return false;

        for (var i = proposed.Count; i < normalized.Count; i++)
            normalized[i] = 0;

        switch (normalized[PARAM_MODE])
        {
            case MODE_MANUAL:
                for (var i = PARAM_MODE + 1; i < normalized.Count; i++)
                    normalized[i] = 0;

                break;
            case MODE_LINEAR:
                normalized[PARAM_LINEAR_MAX_LEVEL] = Math.Max(
                    MIN_LEVELS,
                    normalized[PARAM_LINEAR_MAX_LEVEL]
                );
                normalized[PARAM_EXPONENTIAL_MAX_LEVEL] = 0;

                break;
            case MODE_EXPONENTIAL:
                normalized[PARAM_EXPONENTIAL_FACTOR] = Math.Max(
                    MIN_FACTOR,
                    normalized[PARAM_EXPONENTIAL_FACTOR]
                );
                normalized[PARAM_EXPONENTIAL_MAX_LEVEL] = Math.Max(
                    MIN_LEVELS,
                    normalized[PARAM_EXPONENTIAL_MAX_LEVEL]
                );

                break;
        }

        return true;
    }

    public override async Task<bool> ApplyWiredUpdateAsync(
        ActionContext ctx,
        UpdateWiredMessage update,
        CancellationToken ct
    ) =>
        await base.ApplyWiredUpdateAsync(
            ctx,
            update with
            {
                StringParam = IsLevelTable(update.StringParam) ? update.StringParam : string.Empty,
            },
            ct
        );

    /// <summary>Whether every line of a manual table is "level=xp" in digits, one per "\n".</summary>
    private static bool IsLevelTable(string? text) =>
        !string.IsNullOrEmpty(text)
        && text.Split('\n')
            .All(line =>
                line.Split('=') is [var level, var xp]
                && level.Length > 0
                && xp.Length > 0
                && level.All(char.IsAsciiDigit)
                && xp.All(char.IsAsciiDigit)
            );

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
        var mask = GetIntParamOrDefault(0, 0);

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
        if (
            !parent.TryGetValue(key, out var xpValue)
            || !TryGetLevelProgress(xpValue.Value, out var reached)
        )
            return null;

        long xp = Math.Max(0, xpValue.Value);
        var (level, maxLevel, levelStart, nextStart, isMaxed) = reached;
        var required = Math.Max(0, nextStart - levelStart);
        var progress = Math.Max(0, xp - levelStart);

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

        return new WiredVariableValue(result);
    }

    /// <summary>
    /// The level an experience total has reached by this box's table. The sub-variables are
    /// derived from it, and so is what a levelling fx on the same tile shows.
    /// </summary>
    public bool TryGetLevelProgress(long xp, out WiredLevelProgress progress)
    {
        progress = default;

        if (_thresholds.Count == 0)
            return false;

        xp = Math.Max(0, xp);

        var maxLevel = _thresholds.Count;
        var level = 1;

        while (level < maxLevel && xp >= _thresholds[level])
            level++;

        var levelStart = _thresholds[level - 1];

        progress = new WiredLevelProgress(
            level,
            maxLevel,
            levelStart,
            level < maxLevel ? _thresholds[level] : levelStart,
            level >= maxLevel
        );

        return true;
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
                        && int.TryParse(
                            parts[0].Trim(),
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out var level
                        )
                        && long.TryParse(
                            parts[1].Trim(),
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out var xp
                        )
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
