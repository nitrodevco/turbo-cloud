using System.Collections.Frozen;
using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Rooms.Wired.VariableFx;

/// <summary>
/// The styles the client knows per fx category, copied from its style table (the
/// <c>variablefx</c> preview style definitions). It is not decoration: the client looks a
/// config's (category, style) pair up and dereferences the result unchecked, so one pair that is
/// not in this table loses the whole config message, and a renderer that is not registered for
/// the category throws when the first status arrives. A style's extras are what its renderer
/// reads from the config (icon, colour, number design); the client's editor merges them in for
/// its preview, the room has to send them. A colour or width the style does not offer is sent as
/// the style's default, as the client's editor resolves it (<c>VariableFxEditorMetadata.optionIn</c>
/// with the style's <c>defaultColor</c> / <c>defaultWidth</c>, VariableFxEditorState.as:140-142,
/// WIN63-202610201032): a Health Points fx with colour, width and renderer -1 is sent as
/// 1000 / 2 / 10 (official client capture 2026-10-24, wired matrix addon.1200.R2/R5).
/// </summary>
internal static class VariableFxStyles
{
    public const string EXTRA_ICON = "icon";
    public const string EXTRA_ICON_ALIGNMENT = "icon_alignment";
    public const string EXTRA_DESIGN = "design";
    public const string EXTRA_COLOR = "color";
    public const string EXTRA_METALLIC = "metallic";
    public const string EXTRA_SUB_RENDERER = "sub_renderer";
    public const string EXTRA_SEGMENTS = "segments";
    public const string EXTRA_STACK_ORDER = "stack_order";

    public const int RENDERER_CLASSIC_MINI = 1;
    public const int RENDERER_BLOCK = 2;
    public const int RENDERER_STRIPED = 3;
    public const int RENDERER_ARROW = 4;
    public const int RENDERER_THERMOMETER = 13;
    public const int RENDERER_LEVEL_WITH_PROGRESS = 20;
    public const int RENDERER_LEVEL_DETAILS = 21;

    // VariableFxIdResolver's colour and width ids.
    private const int COLOR_NOT_APPLICABLE = -1;
    private const int COLOR_GREEN = 1;
    private const int COLOR_YELLOW = 3;
    private const int COLOR_RED = 5;
    private const int COLOR_BLUE = 7;
    private const int COLOR_WHITE = 19;
    private const int COLOR_DYNAMIC_RED_TO_GREEN = 1000;
    private const int COLOR_DYNAMIC_LEVELLING = 1001;
    private const int COLOR_DYNAMIC_TEAM = 1002;

    private const int WIDTH_NOT_APPLICABLE = -1;
    private const int WIDTH_EXTRA_SMALL = 0;
    private const int WIDTH_SMALL = 1;
    private const int WIDTH_MEDIUM = 2;
    private const int WIDTH_LARGE = 3;
    private const int WIDTH_EXTRA_LARGE = 4;
    private const int WIDTH_BIG_MAHOOSIVE_CHONKY = 100;

    // The colour and width lists of the client's style table (normalColors and the rest).
    private static readonly int[] NORMAL_COLORS =
    [
        1,
        2,
        3,
        4,
        5,
        6,
        7,
        8,
        9,
        10,
        11,
        12,
        13,
        14,
        15,
        16,
        17,
        18,
        19,
        100,
        101,
        102,
        103,
        104,
    ];
    private static readonly int[] ALL_AND_TEAM_COLORS = [.. NORMAL_COLORS, COLOR_DYNAMIC_TEAM];
    private static readonly int[] ALL_AND_RED_TO_GREEN_COLORS =
    [
        .. NORMAL_COLORS,
        COLOR_DYNAMIC_RED_TO_GREEN,
        COLOR_DYNAMIC_TEAM,
    ];
    private static readonly int[] LEVELLING_COLORS =
    [
        .. NORMAL_COLORS,
        COLOR_DYNAMIC_LEVELLING,
        COLOR_DYNAMIC_TEAM,
    ];
    private static readonly int[] RED_TO_GREEN_COLORS = [COLOR_DYNAMIC_RED_TO_GREEN];
    private static readonly int[] NUMBER_TEAM_COLORS =
    [
        COLOR_RED,
        COLOR_GREEN,
        COLOR_BLUE,
        COLOR_YELLOW,
        COLOR_WHITE,
        COLOR_DYNAMIC_TEAM,
    ];
    private static readonly int[] NO_COLORS = [COLOR_NOT_APPLICABLE];

    private static readonly int[] SMALL_TO_LARGE_WIDTHS = [WIDTH_SMALL, WIDTH_MEDIUM, WIDTH_LARGE];
    private static readonly int[] S_TO_XL_WIDTHS =
    [
        WIDTH_SMALL,
        WIDTH_MEDIUM,
        WIDTH_LARGE,
        WIDTH_EXTRA_LARGE,
    ];
    private static readonly int[] XS_TO_XL_WIDTHS =
    [
        WIDTH_EXTRA_SMALL,
        WIDTH_SMALL,
        WIDTH_MEDIUM,
        WIDTH_LARGE,
        WIDTH_EXTRA_LARGE,
    ];
    private static readonly int[] L_TO_XXL_WIDTHS =
    [
        WIDTH_LARGE,
        WIDTH_EXTRA_LARGE,
        WIDTH_BIG_MAHOOSIVE_CHONKY,
    ];
    private static readonly int[] NO_WIDTHS = [WIDTH_NOT_APPLICABLE];

    private static readonly int[] STATUS_BAR_RENDERERS =
    [
        RENDERER_BLOCK,
        RENDERER_STRIPED,
        RENDERER_ARROW,
    ];

    private static readonly FrozenDictionary<
        (VariableFxCategoryType category, int styleId),
        VariableFxStyle
    > STYLES = Build();

    /// <summary>The style, or the category's first one when the id is not in the table.</summary>
    public static VariableFxStyle Get(VariableFxCategoryType category, int styleId) =>
        STYLES.TryGetValue((category, styleId), out var style) ? style : STYLES[(category, 0)];

    // The client's palette entries for the four teams, as the "delegated colour" it expects.
    private static readonly FrozenDictionary<GameTeamType, string> TEAM_COLORS = new Dictionary<
        GameTeamType,
        string
    >
    {
        [GameTeamType.Red] = "#df291e",
        [GameTeamType.Green] = "#36b24a",
        [GameTeamType.Blue] = "#3b7de3",
        [GameTeamType.Yellow] = "#ffd83d",
    }.ToFrozenDictionary();

    /// <summary>The colour the team colour option paints a holder's fx, null for a holder on no team.</summary>
    public static string? GetTeamColor(GameTeamType team) => TEAM_COLORS.GetValueOrDefault(team);

    /// <summary>Renderers that draw a fixed number of segments instead of a continuous bar.</summary>
    public static bool SupportsSegments(int rendererId) =>
        rendererId is RENDERER_BLOCK or RENDERER_ARROW or RENDERER_THERMOMETER;

    private static FrozenDictionary<(VariableFxCategoryType, int), VariableFxStyle> Build()
    {
        var styles = new Dictionary<(VariableFxCategoryType, int), VariableFxStyle>();

        void Add(
            VariableFxCategoryType category,
            int styleId,
            int[] renderers,
            (int[] ids, int fallback) colors,
            (int[] ids, int fallback) widths,
            params (string key, string value)[] extra
        ) =>
            styles[(category, styleId)] = new VariableFxStyle(
                styleId,
                renderers,
                colors.ids,
                colors.fallback,
                widths.ids,
                widths.fallback,
                extra
            );

        Add(
            VariableFxCategoryType.HealthPoints,
            0,
            [10],
            (RED_TO_GREEN_COLORS, COLOR_DYNAMIC_RED_TO_GREEN),
            (SMALL_TO_LARGE_WIDTHS, WIDTH_MEDIUM),
            (EXTRA_ICON, "misc_heart")
        );
        Add(
            VariableFxCategoryType.HealthPoints,
            1,
            [12],
            (ALL_AND_TEAM_COLORS, COLOR_RED),
            ([WIDTH_MEDIUM, WIDTH_LARGE], WIDTH_MEDIUM)
        );
        Add(
            VariableFxCategoryType.HealthPoints,
            2,
            [RENDERER_THERMOMETER],
            (NO_COLORS, COLOR_NOT_APPLICABLE),
            (XS_TO_XL_WIDTHS, WIDTH_MEDIUM)
        );
        Add(
            VariableFxCategoryType.HealthPoints,
            3,
            [11],
            (ALL_AND_TEAM_COLORS, COLOR_RED),
            (NO_WIDTHS, WIDTH_NOT_APPLICABLE)
        );

        int[] progressRenderers =
        [
            0,
            RENDERER_BLOCK,
            RENDERER_STRIPED,
            RENDERER_ARROW,
            RENDERER_CLASSIC_MINI,
        ];

        for (var styleId = 0; styleId < progressRenderers.Length; styleId++)
            Add(
                VariableFxCategoryType.ProgressBar,
                styleId,
                [progressRenderers[styleId]],
                (ALL_AND_TEAM_COLORS, COLOR_GREEN),
                (XS_TO_XL_WIDTHS, WIDTH_MEDIUM)
            );

        Add(
            VariableFxCategoryType.LevellingProgress,
            0,
            [RENDERER_LEVEL_WITH_PROGRESS],
            (LEVELLING_COLORS, COLOR_DYNAMIC_LEVELLING),
            (SMALL_TO_LARGE_WIDTHS, WIDTH_MEDIUM)
        );
        Add(
            VariableFxCategoryType.LevellingProgress,
            1,
            [RENDERER_LEVEL_DETAILS],
            (LEVELLING_COLORS, COLOR_DYNAMIC_LEVELLING),
            (S_TO_XL_WIDTHS, WIDTH_MEDIUM)
        );

        // Status bars: a themed icon and a baked colour. The battery runs red to green instead.
        (string icon, string? color, bool metallic)[] statusBars =
        [
            ("energy", "#ffd83d", false),
            ("shield", "#4aa9f6", false),
            ("magic", "#8751d1", false),
            ("food", "#ff9f24", false),
            ("stamina", "#86d213", false),
            ("poison", "#8ddc35", false),
            ("mana", "#268fff", false),
            ("health", "#7dce35", false),
            ("gold", "#ffc83d", true),
            ("gems", "#416bdd", true),
            ("honor", "#fac384", false),
            ("reputation", "#ffd83d", false),
            ("cooldown", "#b8c3cc", false),
            ("timeleft", "#74b9e8", false),
            ("burning", "#ff5a1f", false),
            ("freezing", "#82cfff", false),
            ("battery", null, false),
            ("repairing", "#c9c5b8", true),
            ("stealth", "#6254a8", false),
            ("upgrading", "#6bdc34", false),
            ("star_power", "#ffd900", true),
            ("droplet", "#4aabf5", false),
        ];

        for (var styleId = 0; styleId < statusBars.Length; styleId++)
        {
            var (icon, color, metallic) = statusBars[styleId];

            if (color is null)
                Add(
                    VariableFxCategoryType.StatusBar,
                    styleId,
                    STATUS_BAR_RENDERERS,
                    (RED_TO_GREEN_COLORS, COLOR_DYNAMIC_RED_TO_GREEN),
                    (XS_TO_XL_WIDTHS, WIDTH_MEDIUM),
                    (EXTRA_ICON, icon)
                );
            else
                Add(
                    VariableFxCategoryType.StatusBar,
                    styleId,
                    STATUS_BAR_RENDERERS,
                    (NO_COLORS, COLOR_NOT_APPLICABLE),
                    (XS_TO_XL_WIDTHS, WIDTH_MEDIUM),
                    (EXTRA_ICON, icon),
                    (EXTRA_COLOR, color),
                    (EXTRA_METALLIC, metallic ? "true" : "false")
                );
        }

        Add(
            VariableFxCategoryType.BossBar,
            0,
            [100],
            ([COLOR_RED], COLOR_RED),
            (L_TO_XXL_WIDTHS, WIDTH_EXTRA_LARGE),
            (EXTRA_ICON, "misc_skull"),
            (EXTRA_ICON_ALIGNMENT, "double")
        );
        Add(
            VariableFxCategoryType.BossBar,
            1,
            [100],
            (ALL_AND_RED_TO_GREEN_COLORS, COLOR_RED),
            (L_TO_XXL_WIDTHS, WIDTH_EXTRA_LARGE)
        );

        // A number renderer throws without a design it knows.
        Add(
            VariableFxCategoryType.NumberDisplay,
            0,
            [201],
            (NUMBER_TEAM_COLORS, COLOR_GREEN),
            (NO_WIDTHS, WIDTH_NOT_APPLICABLE),
            (EXTRA_DESIGN, "freeze_style")
        );
        Add(
            VariableFxCategoryType.NumberDisplay,
            1,
            [200],
            (ALL_AND_TEAM_COLORS, COLOR_GREEN),
            (NO_WIDTHS, WIDTH_NOT_APPLICABLE),
            (EXTRA_DESIGN, "shalimar")
        );
        Add(
            VariableFxCategoryType.NumberDisplay,
            2,
            [200],
            (ALL_AND_TEAM_COLORS, COLOR_GREEN),
            (NO_WIDTHS, WIDTH_NOT_APPLICABLE),
            (EXTRA_DESIGN, "blocky")
        );

        return styles.ToFrozenDictionary();
    }
}
