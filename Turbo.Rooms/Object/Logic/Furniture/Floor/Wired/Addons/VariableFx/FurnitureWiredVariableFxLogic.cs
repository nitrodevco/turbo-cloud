using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots.Wired.VariableFx;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired.Rules;
using Turbo.Rooms.Wired.VariableFx;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons.VariableFx;

/// <summary>
/// A variable fx addon: it draws the variable box it shares a tile with over every avatar or
/// furni that holds the variable (a health bar, a level badge, a number...). The box holds no
/// state and sends nothing itself. It knows how its fx looks (<see cref="BuildConfig"/>), what
/// one holder shows (<see cref="ResolveStatus"/>) and who may see it
/// (<see cref="Visibility"/>); <see cref="RoomVariableFxSystem"/> does the rest.
///
/// The editor never picks the variable, only the two that may replace the value range and the
/// one that gates the audience, so which variable an fx shows is the room's rule: the box on the
/// same tile, as for the other variable addons.
///
/// Int params, as the client's variable fx editor writes them: source (furni or user),
/// visibility, show mode, update mask, show on hover, show duration, style, colour, width,
/// renderer, default min (long), default max (long), override min enabled, override max
/// enabled, override min target, override max target, audience value (long), segments. Two
/// categories add one more. Variable ids are positional: override min, override max, audience.
/// </summary>
public abstract class FurnitureWiredVariableFxLogic(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int PARAM_SOURCE = 0;
    private const int PARAM_VISIBILITY = 1;
    private const int PARAM_SHOW_MODE = 2;
    private const int PARAM_UPDATE_MASK = 3;
    private const int PARAM_SHOW_ON_HOVER = 4;
    private const int PARAM_SHOW_DURATION = 5;
    private const int PARAM_STYLE = 6;
    private const int PARAM_COLOR = 7;
    private const int PARAM_WIDTH = 8;
    private const int PARAM_RENDERER = 9;
    private const int PARAM_DEFAULT_MIN = 10;
    private const int PARAM_DEFAULT_MAX = 12;
    private const int PARAM_OVERRIDE_MIN_ENABLED = 14;
    private const int PARAM_OVERRIDE_MAX_ENABLED = 15;
    private const int PARAM_OVERRIDE_MIN_TARGET = 16;
    private const int PARAM_OVERRIDE_MAX_TARGET = 17;
    private const int PARAM_AUDIENCE_VALUE = 18;
    private const int PARAM_SEGMENTS = 20;

    /// <summary>The first int param a category may add after the shared ones.</summary>
    protected const int PARAM_CATEGORY = 21;

    private const int SLOT_OVERRIDE_MIN = 0;
    private const int SLOT_OVERRIDE_MAX = 1;
    private const int SLOT_AUDIENCE = 2;
    private const int SLOT_COUNT = 3;

    // Bounds of the client editor's inputs; its own sanitize applies the same ones.
    private const int UPDATE_MASK_MAX = 15;
    private const int SHOW_DURATION_MIN_MS = 1500;
    private const int SHOW_DURATION_MAX_MS = 20000;
    private const int SHOW_DURATION_DEFAULT_MS = 3000;
    private const int STYLE_MAX = 21;
    private const int COLOR_NOT_APPLICABLE = -1;
    private const int COLOR_MAX = 1002;
    private const int WIDTH_NOT_APPLICABLE = -1;
    private const int WIDTH_MEDIUM = 2;
    private const int WIDTH_MAX = 100;
    private const int RENDERER_MAX = 201;
    private const int SEGMENTS_MAX = 100;
    private const int DEFAULT_MAX_VALUE = 100;

    // Colours the client works out per status, from extras the status has to carry.
    private const int COLOR_DYNAMIC_LEVELLING = 1001;
    private const int COLOR_DYNAMIC_TEAM = 1002;

    protected const string STATUS_CURRENT_LEVEL = "current_level";
    protected const string STATUS_MAX_LEVEL = "max_level";
    protected const string STATUS_IS_MAXED = "is_maxed";
    private const string STATUS_DELEGATED_COLOR = "delegated_color";

    protected abstract VariableFxCategoryType Category { get; }

    /// <summary>False for the categories whose editor has no min and max (levels, plain numbers).</summary>
    protected virtual bool UsesValueRange => true;

    /// <summary>The rules of the int params a category adds after the shared ones.</summary>
    protected virtual IEnumerable<IWiredParamRule> GetCategoryParamRules() => [];

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredEnumParamRule<WiredVariableTargetType>(
                WiredVariableTargetType.User,
                WiredVariableTargetType.Furni,
                WiredVariableTargetType.User
            ),
            new WiredEnumParamRule<VariableFxVisibilityType>(VariableFxVisibilityType.Everyone),
            new WiredEnumParamRule<VariableFxShowModeType>(VariableFxShowModeType.Always),
            new WiredRangeParamRule(0, UPDATE_MASK_MAX, 0),
            new WiredBoolParamRule(false),
            new WiredRangeParamRule(
                SHOW_DURATION_MIN_MS,
                SHOW_DURATION_MAX_MS,
                SHOW_DURATION_DEFAULT_MS
            ),
            new WiredRangeParamRule(0, STYLE_MAX, 0),
            new WiredRangeParamRule(COLOR_NOT_APPLICABLE, COLOR_MAX, COLOR_NOT_APPLICABLE),
            new WiredRangeParamRule(WIDTH_NOT_APPLICABLE, WIDTH_MAX, WIDTH_MEDIUM),
            new WiredRangeParamRule(-1, RENDERER_MAX, 0),
            // Default min and max, each a long: the sign word, then the value.
            WiredRules.AnyInt(),
            WiredRules.AnyInt(),
            WiredRules.AnyInt(),
            WiredRules.AnyInt(DEFAULT_MAX_VALUE),
            new WiredBoolParamRule(false),
            new WiredBoolParamRule(false),
            OverrideTargetRule(),
            OverrideTargetRule(),
            // The value the audience variable must have, a long.
            WiredRules.AnyInt(),
            WiredRules.AnyInt(),
            new WiredRangeParamRule(0, SEGMENTS_MAX, 0),
            .. GetCategoryParamRules(),
        ];

    public override int GetMaxVariableIds() => SLOT_COUNT;

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        AllVariablesContext();

    /// <summary>False until the wired system has loaded the box; its params read as defaults before.</summary>
    public bool IsLoaded => _wiredData is not null;

    /// <summary>Drawn over avatars when true, over furni otherwise.</summary>
    public bool IsUserFx =>
        GetIntParamOrDefault(PARAM_SOURCE, WiredVariableTargetType.User)
        == WiredVariableTargetType.User;

    /// <summary>What the variable this fx shows must hang on for the fx to show anything.</summary>
    public WiredVariableTargetType EntityTargetType =>
        IsUserFx ? WiredVariableTargetType.User : WiredVariableTargetType.Furni;

    /// <summary>
    /// Who is shown the fx. A furni has no owner to be "only" for and no team, so the two
    /// options that need one read as everyone, as in the client's editor.
    /// </summary>
    public VariableFxVisibilityType Visibility
    {
        get
        {
            var visibility = GetIntParamOrDefault(
                PARAM_VISIBILITY,
                VariableFxVisibilityType.Everyone
            );

            return
                !IsUserFx
                && visibility
                    is VariableFxVisibilityType.OnlyUser
                        or VariableFxVisibilityType.GameTeam
                ? VariableFxVisibilityType.Everyone
                : visibility;
        }
    }

    /// <summary>The user variable a viewer must hold for the two "has variable" audiences.</summary>
    public IWiredVariable? GetAudienceVariable()
    {
        var variable = GetVariable(SLOT_AUDIENCE);

        return variable?.GetVarSnapshot().TargetType == WiredVariableTargetType.User
            ? variable
            : null;
    }

    public int AudienceValue => (int)GetLongParam(PARAM_AUDIENCE_VALUE);

    /// <summary>The variable this fx shows: the variable box on its tile, when it fits the source.</summary>
    public FurnitureWiredVariableLogic? GetShownVariable()
    {
        var box = GetVariableBoxOnTile();

        return box?.GetVarSnapshot().TargetType == EntityTargetType ? box : null;
    }

    /// <summary>How the fx looks. Everything in it has been checked against what the client can draw.</summary>
    public VariableFxConfigSnapshot BuildConfig()
    {
        var style = VariableFxStyles.Get(Category, GetIntParamOrDefault(PARAM_STYLE, 0));
        var rendererId = style.ResolveRenderer(GetIntParamOrDefault(PARAM_RENDERER, 0));
        var (defaultMin, defaultMax) = GetDefaultRange();
        var extra = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var (key, value) in style.Extra)
            extra[key] = value;

        AddCategoryExtra(extra, style, rendererId);

        var segments = GetIntParamOrDefault(PARAM_SEGMENTS, 0);

        if (segments > 0 && VariableFxStyles.SupportsSegments(GetSegmentRendererId(rendererId)))
            extra[VariableFxStyles.EXTRA_SEGMENTS] = segments.ToString(
                CultureInfo.InvariantCulture
            );

        return new VariableFxConfigSnapshot
        {
            ConfigId = ObjectId,
            IsUserFx = IsUserFx,
            ShowMode = GetIntParamOrDefault(PARAM_SHOW_MODE, VariableFxShowModeType.Always),
            UpdateMask = GetIntParamOrDefault(PARAM_UPDATE_MASK, 0),
            ShowOnMouseHover = GetIntParamOrDefault(PARAM_SHOW_ON_HOVER, false),
            ShowDurationMs = GetIntParamOrDefault(PARAM_SHOW_DURATION, SHOW_DURATION_DEFAULT_MS),
            Category = Category,
            StyleId = style.StyleId,
            ColorId = GetIntParamOrDefault(PARAM_COLOR, COLOR_NOT_APPLICABLE),
            WidthId = GetIntParamOrDefault(PARAM_WIDTH, WIDTH_MEDIUM),
            RendererId = rendererId,
            DefaultMinValue = defaultMin,
            DefaultMaxValue = defaultMax,
            Extra = extra.ToImmutableDictionary(),
        };
    }

    /// <summary>
    /// What one holder of the variable shows: its value, the range when it is not the config's
    /// default one, and the extras the client's renderer reads per status.
    /// <paramref name="teamColor"/> is the holder's game team colour, for the team colour option.
    /// </summary>
    public virtual VariableFxStatusSnapshot ResolveStatus(
        VariableFxStatusKeySnapshot key,
        int targetId,
        long value,
        string? teamColor
    )
    {
        var extra = new SortedDictionary<string, string>(StringComparer.Ordinal);

        AddColorExtra(extra, teamColor);

        var range = ResolveOverriddenRange(targetId);

        return new VariableFxStatusSnapshot
        {
            Key = key,
            IsInitialize = false,
            Value = value,
            OverrideMinValue = range?.min,
            OverrideMaxValue = range?.max,
            Extra = extra.ToImmutableDictionary(),
        };
    }

    /// <summary>Config extras only this category has.</summary>
    protected virtual void AddCategoryExtra(
        IDictionary<string, string> extra,
        VariableFxStyle style,
        int rendererId
    ) { }

    /// <summary>The renderer a segment count applies to; a level badge passes it to its bar.</summary>
    protected virtual int GetSegmentRendererId(int rendererId) => rendererId;

    /// <summary>
    /// The extras the two per-status colours need. The levelling colour reads the level from
    /// every status and the client does not check that it is there, so a fx that is not about
    /// levels still has to say "level one of one".
    /// </summary>
    protected void AddColorExtra(IDictionary<string, string> extra, string? teamColor)
    {
        var colorId = GetIntParamOrDefault(PARAM_COLOR, COLOR_NOT_APPLICABLE);

        if (colorId == COLOR_DYNAMIC_TEAM && teamColor is not null)
            extra[STATUS_DELEGATED_COLOR] = teamColor;

        if (colorId == COLOR_DYNAMIC_LEVELLING)
            AddLevelExtra(extra, level: 1, maxLevel: 1, isMaxed: false);
    }

    protected static void AddLevelExtra(
        IDictionary<string, string> extra,
        int level,
        int maxLevel,
        bool isMaxed
    )
    {
        extra[STATUS_CURRENT_LEVEL] = level.ToString(CultureInfo.InvariantCulture);
        extra[STATUS_MAX_LEVEL] = maxLevel.ToString(CultureInfo.InvariantCulture);
        extra[STATUS_IS_MAXED] = isMaxed ? "true" : "false";
    }

    /// <summary>
    /// The config's range, repaired the way the client's editor repairs it: a maximum that is
    /// not above the minimum would divide by zero in every bar.
    /// </summary>
    protected virtual (long min, long max) GetDefaultRange()
    {
        if (!UsesValueRange)
            return (0, DEFAULT_MAX_VALUE);

        var min = GetLongParam(PARAM_DEFAULT_MIN);
        var max = GetLongParam(PARAM_DEFAULT_MAX);

        if (max <= min)
            max = min < DEFAULT_MAX_VALUE ? DEFAULT_MAX_VALUE : min + 1;

        return (min, max);
    }

    /// <summary>
    /// The holder's own range when a variable replaces either end of it, null when neither
    /// does. The client takes the two ends as a pair, so the end without a variable repeats the
    /// default.
    /// </summary>
    private (long min, long max)? ResolveOverriddenRange(int targetId)
    {
        if (!UsesValueRange)
            return null;

        var min = ReadOverride(
            PARAM_OVERRIDE_MIN_ENABLED,
            PARAM_OVERRIDE_MIN_TARGET,
            SLOT_OVERRIDE_MIN,
            targetId
        );
        var max = ReadOverride(
            PARAM_OVERRIDE_MAX_ENABLED,
            PARAM_OVERRIDE_MAX_TARGET,
            SLOT_OVERRIDE_MAX,
            targetId
        );

        if (min is null && max is null)
            return null;

        var (defaultMin, defaultMax) = GetDefaultRange();

        return (min ?? defaultMin, max ?? defaultMax);
    }

    /// <summary>
    /// One end of the range from a variable: the holder's own value of it, or a room-wide
    /// variable. Null when the option is off, or the variable is gone, hangs on something else
    /// or has no value for this holder.
    /// </summary>
    private long? ReadOverride(int enabledParam, int targetParam, int slot, int targetId)
    {
        if (!GetIntParamOrDefault(enabledParam, false) || GetVariable(slot) is not { } variable)
            return null;

        var isGlobal =
            GetIntParamOrDefault(targetParam, EntityTargetType) == WiredVariableTargetType.Global;
        var targetType = isGlobal ? WiredVariableTargetType.Global : EntityTargetType;
        var snapshot = variable.GetVarSnapshot();

        if (snapshot.TargetType != targetType)
            return null;

        var key = new WiredVariableKey(snapshot.VariableId, targetType, isGlobal ? 0 : targetId);

        return variable.TryGetValue(key, out var value) ? value.Value : null;
    }

    // Override min, override max, audience: see HasPositionalVariableIds.
    protected override bool HasPositionalVariableIds => true;

    // A save, a move, placing and picking up all end here. The stack event stays: it is what
    // makes the wired system load this box. The fx system is told besides, since every one of
    // them changes what it shows.
    protected override async Task OnWiredStackChangedAsync(
        ActionContext ctx,
        List<int> ids,
        CancellationToken ct
    )
    {
        await base.OnWiredStackChangedAsync(ctx, ids, ct);

        await _ctx.PublishRoomEventAsync(
            new WiredVariableFxBoxChangedEvent { RoomId = _ctx.RoomId, CausedBy = ctx },
            ct
        );
    }

    private static WiredEnumParamRule<WiredVariableTargetType> OverrideTargetRule() =>
        new(
            WiredVariableTargetType.User,
            WiredVariableTargetType.Furni,
            WiredVariableTargetType.User,
            WiredVariableTargetType.Global
        );
}
