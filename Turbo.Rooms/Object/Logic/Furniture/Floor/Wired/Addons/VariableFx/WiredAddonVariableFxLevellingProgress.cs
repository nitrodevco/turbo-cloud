using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.VariableFx;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;
using Turbo.Rooms.Wired.VariableFx;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons.VariableFx;

/// <summary>
/// A level badge with a progress bar for the experience variable on this tile. The client does
/// not work levels out: every status tells it the level, the level cap and whether the cap is
/// reached, and carries the experience at which the current level and the next one start as its
/// range. Those come from the level-up addon on the same tile (<c>wf_xtra_var_lvlup_system</c>);
/// without one the variable is shown as level one, 0 to 100. The extra int param is the bar the
/// badge style draws beside it.
/// </summary>
[RoomObjectLogic("wf_xtra_var_fx_level")]
public class WiredAddonVariableFxLevellingProgress(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredVariableFxLogic(grainFactory, stuffDataFactory, ctx)
{
    // The bars the client's editor offers beside the badge; the details style always uses the
    // mini bar.
    private static readonly int[] BADGE_BARS =
    [
        VariableFxStyles.RENDERER_BLOCK,
        VariableFxStyles.RENDERER_STRIPED,
        VariableFxStyles.RENDERER_ARROW,
    ];

    public override int WiredCode => (int)WiredAddonType.VARIABLE_FX_LEVELLING_PROGRESS;

    protected override VariableFxCategoryType Category => VariableFxCategoryType.LevellingProgress;

    protected override bool UsesValueRange => false;

    protected override IEnumerable<IWiredParamRule> GetCategoryParamRules() =>
        [
            new WiredRangeParamRule(
                -1,
                VariableFxStyles.RENDERER_ARROW,
                VariableFxStyles.RENDERER_BLOCK
            ),
        ];

    protected override void AddCategoryExtra(
        IDictionary<string, string> extra,
        VariableFxStyle style,
        int rendererId
    ) =>
        extra[VariableFxStyles.EXTRA_SUB_RENDERER] = GetSegmentRendererId(rendererId)
            .ToString(CultureInfo.InvariantCulture);

    protected override int GetSegmentRendererId(int rendererId)
    {
        if (rendererId != VariableFxStyles.RENDERER_LEVEL_WITH_PROGRESS)
            return VariableFxStyles.RENDERER_CLASSIC_MINI;

        var chosen = GetIntParamOrDefault(PARAM_CATEGORY, VariableFxStyles.RENDERER_BLOCK);

        return Array.IndexOf(BADGE_BARS, chosen) >= 0 ? chosen : VariableFxStyles.RENDERER_BLOCK;
    }

    public override VariableFxStatusSnapshot ResolveStatus(
        VariableFxStatusKeySnapshot key,
        int targetId,
        long value,
        string? teamColor
    )
    {
        var extra = new SortedDictionary<string, string>(StringComparer.Ordinal);

        AddColorExtra(extra, teamColor);

        // Without a level-up addon on the tile, or with an empty table: level one of one, and
        // the config's own range.
        WiredLevelProgress? reached =
            GetLogicOnTile<WiredAddonVariableLevelUp>() is { } levelUp
            && levelUp.TryGetLevelProgress(value, out var progress)
                ? progress
                : null;

        AddLevelExtra(
            extra,
            reached?.Level ?? 1,
            reached?.MaxLevel ?? 1,
            reached?.IsMaxed ?? false
        );

        // At the cap both ends are the last level's start: with "is maxed" that is how the
        // client knows to draw the bar full instead of empty.
        return new VariableFxStatusSnapshot
        {
            Key = key,
            IsInitialize = false,
            Value = value,
            OverrideMinValue = reached?.LevelStartXp,
            OverrideMaxValue = reached?.NextLevelXp,
            Extra = extra.ToImmutableDictionary(),
        };
    }
}
