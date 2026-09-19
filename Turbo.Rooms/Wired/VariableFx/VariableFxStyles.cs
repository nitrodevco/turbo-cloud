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
/// its preview, the room has to send them.
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

    public const int RENDERER_CLASSIC_MINI = 1;
    public const int RENDERER_BLOCK = 2;
    public const int RENDERER_STRIPED = 3;
    public const int RENDERER_ARROW = 4;
    public const int RENDERER_THERMOMETER = 13;
    public const int RENDERER_LEVEL_WITH_PROGRESS = 20;
    public const int RENDERER_LEVEL_DETAILS = 21;

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
            params (string key, string value)[] extra
        ) => styles[(category, styleId)] = new VariableFxStyle(styleId, renderers, extra);

        Add(VariableFxCategoryType.HealthPoints, 0, [10], (EXTRA_ICON, "misc_heart"));
        Add(VariableFxCategoryType.HealthPoints, 1, [12]);
        Add(VariableFxCategoryType.HealthPoints, 2, [RENDERER_THERMOMETER]);
        Add(VariableFxCategoryType.HealthPoints, 3, [11]);

        Add(VariableFxCategoryType.ProgressBar, 0, [0]);
        Add(VariableFxCategoryType.ProgressBar, 1, [RENDERER_BLOCK]);
        Add(VariableFxCategoryType.ProgressBar, 2, [RENDERER_STRIPED]);
        Add(VariableFxCategoryType.ProgressBar, 3, [RENDERER_ARROW]);
        Add(VariableFxCategoryType.ProgressBar, 4, [RENDERER_CLASSIC_MINI]);

        Add(VariableFxCategoryType.LevellingProgress, 0, [RENDERER_LEVEL_WITH_PROGRESS]);
        Add(VariableFxCategoryType.LevellingProgress, 1, [RENDERER_LEVEL_DETAILS]);

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
                    (EXTRA_ICON, icon)
                );
            else
                Add(
                    VariableFxCategoryType.StatusBar,
                    styleId,
                    STATUS_BAR_RENDERERS,
                    (EXTRA_ICON, icon),
                    (EXTRA_COLOR, color),
                    (EXTRA_METALLIC, metallic ? "true" : "false")
                );
        }

        Add(
            VariableFxCategoryType.BossBar,
            0,
            [100],
            (EXTRA_ICON, "misc_skull"),
            (EXTRA_ICON_ALIGNMENT, "double")
        );
        Add(VariableFxCategoryType.BossBar, 1, [100]);

        // A number renderer throws without a design it knows.
        Add(VariableFxCategoryType.NumberDisplay, 0, [201], (EXTRA_DESIGN, "freeze_style"));
        Add(VariableFxCategoryType.NumberDisplay, 1, [200], (EXTRA_DESIGN, "shalimar"));
        Add(VariableFxCategoryType.NumberDisplay, 2, [200], (EXTRA_DESIGN, "blocky"));

        return styles.ToFrozenDictionary();
    }
}
