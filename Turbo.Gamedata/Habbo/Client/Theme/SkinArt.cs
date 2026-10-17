using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Turbo.Assets.Conversion;

namespace Turbo.Gamedata.Habbo.Client.Theme;

/// <summary>The renderer's scale modes (<c>SkinLayoutEntity</c>); anything else, <c>stretch</c> included, is fixed.</summary>
internal enum SkinScale
{
    Fixed,
    Move,
    Strech,
    Tiled,
    Center,
}

/// <summary>One entity of a skin state, as the renderer draws it.</summary>
/// <param name="at">Where the layout puts it at the skin's own size.</param>
/// <param name="from">Its region of the sheet.</param>
/// <param name="shade">The <c>HsvLayerColor</c> shade of an <c>hsv_layer</c> entity; null for a plain multiply.</param>
internal sealed class SkinPiece(
    string name,
    ThemeRect at,
    ThemeRect from,
    SkinScale h,
    SkinScale v,
    bool colorize,
    double? shade
)
{
    public string Name { get; } = name;

    public ThemeRect At { get; } = at;

    public ThemeRect From { get; } = from;

    public SkinScale H { get; } = h;

    public SkinScale V { get; } = v;

    public bool Colorize { get; } = colorize;

    public double? Shade { get; } = shade;

    public bool IsTiled => H == SkinScale.Tiled || V == SkinScale.Tiled;

    public bool IsCentered => H == SkinScale.Center || V == SkinScale.Center;

    public SkinPiece WithScales(SkinScale h, SkinScale v) =>
        new(Name, At, From, h, v, Colorize, Shade);
}

/// <summary>One interaction state of a skin, at the layout an element row names.</summary>
/// <param name="Layout">The skin layout it is drawn with: several share one skin (the scrollbar's track, lift and arrows).</param>
/// <param name="Sheet">The sheet asset the template cuts from.</param>
internal sealed record SkinState(
    string Name,
    string? Layout,
    string Sheet,
    double Width,
    double Height,
    List<SkinPiece> Pieces
);

/// <summary>A skin entity that plays a part of its own in a component (a dropmenu's <c>arrow</c>), cut and placed.</summary>
internal sealed record SkinRolePiece(
    string TextureKey,
    double Left,
    double Top,
    double Right,
    double Bottom,
    double Width,
    double Height
);

/// <summary>A variant's art: its layer fields, and the sprites they name by asset.</summary>
internal sealed record SkinVariantArt(JsonObject Fields, Dictionary<string, RgbaImage> Sprites);

/// <summary>
/// A variant's art, read off its skin the way the client's <c>BitmapSkinRenderer</c> draws it, as
/// Studio's <c>server/theme/skinArt.ts</c> builds it: the layers the theme draws
/// (<c>BackgroundLayerConfig</c>s) and the sprites they name, cut out of the skin's sheet.
/// <para>
/// A skin state is a template (regions of the sheet) and a layout (where each region sits at the
/// skin's own size, and how it follows a resize on each axis). The entities of a state are grouped
/// by what they take (the window's tint, nothing, or an HSV shade) and by their name's prefix (a
/// frame's <c>shine_*</c> against its body), and each group becomes a layer: a grid over the whole
/// skin is a nine-slice, one entity over the whole skin a sprite, <c>hsv_layer</c> entities one
/// nine-slice per shade, and anything else a composite of pieces each placed as the renderer
/// places it.
/// </para>
/// </summary>
internal static partial class SkinArt
{
    /// <summary>The theme's interaction states, from the skin's; <c>active</c> has no counterpart.</summary>
    private static readonly string[] THEME_STATES =
    [
        "default",
        "hovering",
        "pressed",
        "disabled",
        "selected",
    ];

    /// <summary>The components the theme draws per interaction state (<c>ThemeWithStatesVariant</c>); the rest draw one layer.</summary>
    private static readonly HashSet<string> STATEFUL_COMPONENTS =
    [
        "button",
        "buttonGroupCenter",
        "buttonGroupLeft",
        "buttonGroupRight",
        "buttonThick",
        "checkBox",
        "closeButton",
        "containerButton",
        "droplistItem",
        "dropmenuItem",
        "iconButton",
        "radioButton",
        "scrollbarSliderBarHorizontal",
        "scrollbarSliderBarVertical",
        "scrollbarSliderButtonDown",
        "scrollbarSliderButtonLeft",
        "scrollbarSliderButtonRight",
        "scrollbarSliderButtonUp",
        "scrollbarSliderTrackHorizontal",
        "scrollbarSliderTrackVertical",
        "tabButton",
    ];

    /// <summary>
    /// The one-layer components whose box takes its size from their art (a scaler's grip, a frame's
    /// pointer); every stateful one does too.
    /// </summary>
    private static readonly HashSet<string> ART_SIZED_COMPONENTS =
    [
        "bubblePointerDown",
        "bubblePointerLeft",
        "bubblePointerRight",
        "bubblePointerUp",
        "framePointerDown",
        "scaler",
    ];

    /// <summary>The components that draw a <c>plain</c> layer of their own between their layer and overlay.</summary>
    private static readonly HashSet<string> PLAIN_COMPONENTS = ["frame"];

    /// <summary>A texture key, as the theme names one: the asset less <c>theme-</c>, and <c>-src</c>.</summary>
    public static string TextureKeyOf(string asset) =>
        $"{(asset.StartsWith("theme-", StringComparison.Ordinal) ? asset[6..] : asset)}-src";

    /// <summary>A variant's art from the skin its element row names; null when it has none.</summary>
    public static SkinVariantArt? DeriveArt(
        ThemeLibrary library,
        string component,
        ThemeElementRow row,
        ComponentAdapter? adapter
    )
    {
        var skin = row.Skin is null ? null : library.Skin(row.Skin);

        if (skin is null)
            return null;

        var sprites = new Dictionary<string, RgbaImage>();
        var fields = new JsonObject();
        var roles = new Dictionary<string, Dictionary<string, SkinRolePiece>>();
        var states = new List<SkinState>();

        // The entities the component draws itself are cut on their own and left out of every layer.
        foreach (var state in SkinStates(skin, row.Layout))
        {
            var sheet = state.Sheet.Length > 0 ? library.Sheet(state.Sheet) : null;
            var own = adapter is null
                ? []
                : state.Pieces.Where(x => adapter.Roles.Contains(x.Name)).ToList();

            if (own.Count == 0 || sheet is null)
            {
                states.Add(state);

                continue;
            }

            var stateRoles = new Dictionary<string, SkinRolePiece>();

            foreach (var piece in own)
            {
                var asset = AssetName(SkinStem(skin), state.Layout ?? "", state.Name, piece.Name);
                var at = NaturalRect(piece, state.Width, state.Height);

                sprites[asset] = CutPiece(sheet, piece);
                stateRoles[piece.Name] = new SkinRolePiece(
                    TextureKeyOf(asset),
                    at.X,
                    at.Y,
                    state.Width - (at.X + at.Width),
                    state.Height - (at.Y + at.Height),
                    at.Width,
                    at.Height
                );
            }

            roles[state.Name] = stateRoles;
            states.Add(state with { Pieces = [.. state.Pieces.Where(x => !own.Contains(x))] });
        }

        if (states.Count == 0)
            return null;

        if (adapter is not null)
            ThemeJson.Assign(fields, adapter.Fields(roles));

        if (STATEFUL_COMPONENTS.Contains(component))
        {
            var layers = new JsonObject();
            var overlays = new JsonObject();

            foreach (var state in states)
            {
                var drawn = StateLayers(library, skin, state, true);
                // A stateful component draws its untinted pieces over its state's layer as one overlay.
                var overlay =
                    drawn.Plain is not null && drawn.Overlay is not null
                        ? drawn.Untinted?.Invoke()
                        : drawn.Plain ?? drawn.Overlay;

                foreach (var (asset, image) in drawn.Sprites)
                    sprites[asset] = image;

                if (drawn.Tinted is not null)
                    layers[state.Name] = drawn.Tinted;

                if (overlay is not null)
                    overlays[state.Name] = overlay;
            }

            if (layers.Count > 0)
                fields["states"] = layers;

            if (overlays.Count > 0)
                fields["overlays"] = overlays;

            // Every piece untintable: the window's colour has nothing to reach.
            if (layers.Count == 0)
                fields["colorize"] = false;
        }
        else
        {
            var state = states.FirstOrDefault(x => x.Name == "default") ?? states[0];
            var drawn = StateLayers(library, skin, state, ART_SIZED_COMPONENTS.Contains(component));
            var merged = drawn.Untinted?.Invoke();

            foreach (var (asset, image) in drawn.Sprites)
                sprites[asset] = image;

            if (drawn.Tinted is not null)
                fields["layer"] = drawn.Tinted;
            else if (drawn.Plain is not null)
            {
                fields["layer"] = drawn.Plain;
                fields["colorize"] = false;
            }

            // Only a frame draws a `plain` layer; every other component draws its untinted pieces as
            // its overlay (a header's `header_shine`), over the tinted layer all the same.
            var untinted = drawn.Tinted is not null ? drawn.Plain : null;

            if (untinted is not null && PLAIN_COMPONENTS.Contains(component))
            {
                fields["plain"] = untinted;

                if (drawn.Overlay is not null)
                    fields["overlay"] = drawn.Overlay;
            }
            else
            {
                var overlay =
                    untinted is not null && drawn.Overlay is not null
                        ? merged
                        : untinted ?? drawn.Overlay;

                if (overlay is not null)
                    fields["overlay"] = overlay.Parent is null ? overlay : overlay.DeepClone();
            }
        }

        // Only the sprites the fields name: a group re-derived as one overlay leaves its parts' unused.
        var named = ThemeJson.Strings(fields);

        return new SkinVariantArt(
            fields,
            sprites
                .Where(x => named.Contains(TextureKeyOf(x.Key)))
                .ToDictionary(x => x.Key, x => x.Value)
        );
    }

    /* ---------------------------------------------------------------- states */

    /// <summary>A skin's states for the layout an element row names (every layout when the row names none the skin has).</summary>
    private static List<SkinState> SkinStates(ThemeSkin skin, string? layoutName)
    {
        var named = skin.States.Where(x => x.Attr("layout") == layoutName).ToList();
        var result = new List<SkinState>();

        foreach (var state in named.Count > 0 ? named : skin.States)
        {
            var name = state.Attr("name");

            if (name is null || !THEME_STATES.Contains(name) || result.Any(x => x.Name == name))
                continue;

            if (
                !skin.Templates.TryGetValue(
                    ThemeLibrary.Key(state.Attr("template")),
                    out var template
                )
                || !skin.Layouts.TryGetValue(ThemeLibrary.Key(state.Attr("layout")), out var layout)
            )
                continue;

            var regions = new Dictionary<string, ThemeRect?>();

            foreach (var entity in template.Child("entities")?.ChildrenOf("entity") ?? [])
                regions[ThemeLibrary.Key(entity.Attr("name"))] = ThemeLibrary.SkinRect(
                    entity,
                    skin.Variables
                );

            var pieces = new List<SkinPiece>();

            foreach (var entity in layout.Child("entities")?.ChildrenOf("entity") ?? [])
            {
                var at = ThemeLibrary.SkinRect(entity, skin.Variables);
                var from = regions.GetValueOrDefault(ThemeLibrary.Key(entity.Attr("name")));

                // An entity the template does not name draws nothing.
                if (at is null || from is null)
                    continue;

                var colorize = entity.Attr("colorize") != "false";

                pieces.Add(
                    new SkinPiece(
                        ThemeLibrary.Key(entity.Attr("name")),
                        at.Value,
                        from.Value,
                        ScaleOf(entity, "horizontal"),
                        ScaleOf(entity, "vertical"),
                        colorize,
                        colorize && entity.Attr("colorizeMethod") == "hsv_layer"
                            ? ThemeNumbers.Parse(entity.Attr("shade") ?? "0")
                            : null
                    )
                );
            }

            var asset = SkinVariable()
                .Replace(
                    template.Attr("asset") ?? "",
                    match => skin.Variables.GetValueOrDefault(match.Groups[1].Value) ?? "",
                    1
                );

            result.Add(
                new SkinState(
                    name,
                    state.Attr("layout"),
                    asset,
                    pieces.Aggregate(0d, (max, x) => Max(max, x.At.X + x.At.Width)),
                    pieces.Aggregate(0d, (max, x) => Max(max, x.At.Y + x.At.Height)),
                    pieces
                )
            );
        }

        return result;
    }

    private static SkinScale ScaleOf(ThemeXmlNode entity, string axis) =>
        (entity.Child("scale")?.Attr(axis) ?? "").ToLowerInvariant() switch
        {
            "move" => SkinScale.Move,
            "strech" => SkinScale.Strech,
            "tiled" => SkinScale.Tiled,
            "center" => SkinScale.Center,
            _ => SkinScale.Fixed,
        };

    /* ---------------------------------------------------------------- drawing */

    /// <summary>Where the renderer draws a piece when the skin is at its own size: <c>center</c> in the middle of it.</summary>
    private static ThemeRect NaturalRect(SkinPiece piece, double width, double height) =>
        new(
            piece.H == SkinScale.Center
                ? Math.Floor((width / 2) - (piece.From.Width / 2))
                : piece.At.X,
            piece.V == SkinScale.Center
                ? Math.Floor((height / 2) - (piece.From.Height / 2))
                : piece.At.Y,
            // `strech` / `tiled` take the layout rect's size; every other mode copies the region's own pixels.
            piece.H
                is SkinScale.Strech
                    or SkinScale.Tiled
                ? piece.At.Width
                : piece.From.Width,
            piece.V is SkinScale.Strech or SkinScale.Tiled ? piece.At.Height : piece.From.Height
        );

    /// <summary>
    /// Where the renderer draws a piece in a window of another size than the skin's: <c>move</c>
    /// shifted by the difference, <c>strech</c> / <c>tiled</c> grown by it, <c>center</c> re-centred.
    /// </summary>
    private static ThemeRect RectAt(SkinPiece piece, SkinState state, double width, double height)
    {
        var natural = NaturalRect(piece, state.Width, state.Height);
        var dx = width - state.Width;
        var dy = height - state.Height;

        return new ThemeRect(
            piece.H == SkinScale.Move ? natural.X + dx
                : piece.H == SkinScale.Center ? Math.Floor((width / 2) - (piece.From.Width / 2))
                : natural.X,
            piece.V == SkinScale.Move ? natural.Y + dy
                : piece.V == SkinScale.Center ? Math.Floor((height / 2) - (piece.From.Height / 2))
                : natural.Y,
            piece.H is SkinScale.Strech or SkinScale.Tiled ? natural.Width + dx : natural.Width,
            piece.V is SkinScale.Strech or SkinScale.Tiled ? natural.Height + dy : natural.Height
        );
    }

    /// <summary>A piece drawn into its rect: its region stretched to it, or (<c>tiled</c>) repeated over it at its own size.</summary>
    private static void DrawPiece(RgbaImage sheet, SkinPiece piece, RgbaImage target, ThemeRect to)
    {
        if (!piece.IsTiled)
        {
            ThemeCanvas.DrawRegion(sheet, piece.From, target, to);

            return;
        }

        var stepX = piece.H == SkinScale.Tiled ? piece.From.Width : to.Width;
        var stepY = piece.V == SkinScale.Tiled ? piece.From.Height : to.Height;

        // A tile of no size would repeat forever; it draws nothing.
        if (!(stepX > 0 && stepY > 0))
            return;

        for (double y = 0; y < to.Height; y += stepY)
        {
            for (double x = 0; x < to.Width; x += stepX)
            {
                var width = Math.Min(stepX, to.Width - x);
                var height = Math.Min(stepY, to.Height - y);
                // A repetition cut short by the rect's end shows the start of the bitmap, not all of it squeezed.
                var from = piece.From with
                {
                    Width = piece.H == SkinScale.Tiled ? width : piece.From.Width,
                    Height = piece.V == SkinScale.Tiled ? height : piece.From.Height,
                };

                ThemeCanvas.DrawRegion(
                    sheet,
                    from,
                    target,
                    new ThemeRect(to.X + x, to.Y + y, width, height)
                );
            }
        }
    }

    /// <summary>A group of pieces as the renderer draws them in a window of the given size.</summary>
    private static RgbaImage ComposeAt(
        RgbaImage sheet,
        List<SkinPiece> pieces,
        SkinState state,
        double width,
        double height
    )
    {
        var image = ThemeCanvas.Blank(width, height);

        foreach (var piece in pieces)
            DrawPiece(sheet, piece, image, RectAt(piece, state, width, height));

        return image;
    }

    /// <summary>A group of pieces composed at the skin's own size.</summary>
    private static RgbaImage ComposeSheet(
        RgbaImage sheet,
        List<SkinPiece> pieces,
        double width,
        double height
    )
    {
        var image = ThemeCanvas.Blank(width, height);

        foreach (var piece in pieces)
            DrawPiece(sheet, piece, image, NaturalRect(piece, width, height));

        return image;
    }

    private static RgbaImage CutPiece(RgbaImage sheet, SkinPiece piece)
    {
        var image = ThemeCanvas.Blank(piece.From.Width, piece.From.Height);

        ThemeCanvas.DrawRegion(
            sheet,
            piece.From,
            image,
            new ThemeRect(0, 0, piece.From.Width, piece.From.Height)
        );

        return image;
    }

    /* ---------------------------------------------------------------- the layers */

    /// <summary>
    /// The nine-slice insets of a group, when it is one: its columns a fixed run, a stretched one and
    /// a moving one (any may be missing), its rows likewise, covering the skin. The grid is the whole
    /// state's (<paramref name="grid"/>), not the group's: a frame's untinted body starts under its
    /// tinted title bar, and sliced on its own pieces the empty rows above it would be the stretched
    /// middle.
    /// </summary>
    private static double[]? NineSliceOf(
        List<SkinPiece> pieces,
        double width,
        double height,
        List<SkinPiece>? grid = null
    )
    {
        grid ??= pieces;

        if (pieces.Count == 0 || pieces.Any(x => x.IsCentered || x.IsTiled))
            return null;

        (double Start, double End)? Edge(bool horizontal, SkinScale mode)
        {
            var spans = grid.Where(x => (horizontal ? x.H : x.V) == mode)
                .Select(x =>
                    horizontal ? (x.At.X, x.At.X + x.At.Width) : (x.At.Y, x.At.Y + x.At.Height)
                )
                .ToList();

            if (spans.Count == 0)
                return null;

            return (
                spans.Aggregate(double.PositiveInfinity, (min, x) => Min(min, x.Item1)),
                spans.Aggregate(double.NegativeInfinity, (max, x) => Max(max, x.Item2))
            );
        }

        var left = Edge(true, SkinScale.Fixed);
        var right = Edge(true, SkinScale.Move);
        var top = Edge(false, SkinScale.Fixed);
        var bottom = Edge(false, SkinScale.Move);
        var leftWidth = left?.End ?? 0;
        var rightWidth = right is { } r ? width - r.Start : 0;
        var topHeight = top?.End ?? 0;
        var bottomHeight = bottom is { } b ? height - b.Start : 0;

        // Every piece has to sit in the cell its modes name: fixed columns left of the stretch, moving ones right of it.
        var fits = pieces.All(piece =>
        {
            var inColumn = piece.H switch
            {
                SkinScale.Fixed => piece.At.X + piece.At.Width <= leftWidth,
                SkinScale.Move => piece.At.X >= width - rightWidth,
                _ => piece.At.X >= leftWidth && piece.At.X + piece.At.Width <= width - rightWidth,
            };
            var inRow = piece.V switch
            {
                SkinScale.Fixed => piece.At.Y + piece.At.Height <= topHeight,
                SkinScale.Move => piece.At.Y >= height - bottomHeight,
                _ => piece.At.Y >= topHeight
                    && piece.At.Y + piece.At.Height <= height - bottomHeight,
            };

            return inColumn && inRow;
        });

        return fits ? [leftWidth, topHeight, rightWidth, bottomHeight] : null;
    }

    /// <summary>The pieces that make a state's grid: every one but those that tile or re-centre on their own.</summary>
    private static List<SkinPiece> GridOf(SkinState state) =>
        [.. state.Pieces.Where(x => !x.IsCentered && !x.IsTiled)];

    /// <summary>A composite piece placed as the renderer places the entity; a <c>tiled</c> axis grows as <c>strech</c> does, its bitmap repeated.</summary>
    private static JsonObject CompositePiece(
        SkinPiece piece,
        string textureKey,
        double width,
        double height
    )
    {
        var field = new JsonObject { ["textureKey"] = textureKey };

        if (piece.IsTiled)
            field["tile"] = true;

        switch (piece.H)
        {
            case SkinScale.Move:
                field["right"] = ThemeNumbers.Json(width - (piece.At.X + piece.At.Width));
                field["width"] = ThemeNumbers.Json(piece.From.Width);
                break;
            case SkinScale.Strech or SkinScale.Tiled:
                field["left"] = ThemeNumbers.Json(piece.At.X);
                field["right"] = ThemeNumbers.Json(width - (piece.At.X + piece.At.Width));
                break;
            case SkinScale.Center:
                field["width"] = ThemeNumbers.Json(piece.From.Width);
                field["alignSelf"] = "center";
                break;
            default:
                field["left"] = ThemeNumbers.Json(piece.At.X);
                field["width"] = ThemeNumbers.Json(piece.From.Width);
                break;
        }

        switch (piece.V)
        {
            case SkinScale.Move:
                field["bottom"] = ThemeNumbers.Json(height - (piece.At.Y + piece.At.Height));
                field["height"] = ThemeNumbers.Json(piece.From.Height);
                break;
            case SkinScale.Strech or SkinScale.Tiled:
                field["top"] = ThemeNumbers.Json(piece.At.Y);
                field["bottom"] = ThemeNumbers.Json(height - (piece.At.Y + piece.At.Height));
                break;
            case SkinScale.Center:
                field["height"] = ThemeNumbers.Json(piece.From.Height);
                field["alignSelf"] = "center";
                break;
            default:
                field["top"] = ThemeNumbers.Json(piece.At.Y);
                field["height"] = ThemeNumbers.Json(piece.From.Height);
                break;
        }

        return field;
    }

    /// <summary>A name's position token taken off: <c>shine_top_left</c> is <c>shine</c>, <c>top_left</c> nothing.</summary>
    private static string GroupOf(SkinPiece piece) => GridToken().Replace(piece.Name, "", 1);

    /// <summary>An asset name of the theme's: <c>theme-</c> and the parts, every <c>_</c> and other character a <c>-</c>.</summary>
    private static string AssetName(params string[] parts) =>
        $"theme-{NotAssetCharacter().Replace(string.Join('-', parts).Replace('_', '-'), "-")}";

    /// <summary>The skin's name as the start of its sprites' names: <c>habbo_skin_button</c> as <c>habbo-button</c>.</summary>
    private static string SkinStem(ThemeSkin skin) => SkinToken().Replace(skin.Name, "-", 1);

    /// <summary>
    /// A grid whose middle tiles on one axis (a scrollbar lift: <c>top</c>, <c>grd</c> tiled down,
    /// <c>btm</c>): a nine-slice repeating its centre, its sheet drawn with the tile once at its own
    /// size. An axis every piece keeps fixed is not sliced.
    /// </summary>
    private static JsonObject? RepeatingNineSlice(
        RgbaImage sheet,
        List<SkinPiece> pieces,
        SkinState state,
        string stem,
        Dictionary<string, RgbaImage> sprites
    )
    {
        var tiled = pieces.Where(x => x.IsTiled).ToList();
        string? axis =
            tiled.All(x => x.V == SkinScale.Tiled && x.H != SkinScale.Tiled) ? "y"
            : tiled.All(x => x.H == SkinScale.Tiled && x.V != SkinScale.Tiled) ? "x"
            : null;

        if (tiled.Count == 0 || axis is null || pieces.Any(x => x.IsCentered))
            return null;

        // One tile size, or the centre is no single bitmap.
        var sizes = tiled
            .Select(x => axis == "y" ? x.From.Height : x.From.Width)
            .Distinct()
            .ToList();

        if (sizes.Count != 1)
            return null;

        var asGrid = pieces
            .Select(x =>
                x.WithScales(
                    x.H == SkinScale.Tiled ? SkinScale.Strech : x.H,
                    x.V == SkinScale.Tiled ? SkinScale.Strech : x.V
                )
            )
            .ToList();
        var insets = NineSliceOf(asGrid, state.Width, state.Height);

        if (insets is null)
            return null;

        var (left, top, right, bottom) = (insets[0], insets[1], insets[2], insets[3]);
        var fixedAcross = pieces.All(x => x.H == SkinScale.Fixed);
        var fixedDown = pieces.All(x => x.V == SkinScale.Fixed);
        var (leftWidth, rightWidth) = fixedAcross ? (0d, 0d) : (left, right);
        var (topHeight, bottomHeight) = fixedDown ? (0d, 0d) : (top, bottom);

        // The tiles must be the whole centre on their axis, or the repeat would reach pieces they don't.
        var centred = tiled.All(x =>
            axis == "y"
                ? x.At.Y == top && x.At.Y + x.At.Height == state.Height - bottom
                : x.At.X == left && x.At.X + x.At.Width == state.Width - right
        );

        if (
            !centred
            || leftWidth + rightWidth >= state.Width
            || topHeight + bottomHeight >= state.Height
        )
            return null;

        var tile = sizes[0];
        var width = axis == "x" ? left + tile + right : state.Width;
        var height = axis == "y" ? top + tile + bottom : state.Height;
        var asset = AssetName(stem);

        sprites[asset] = ComposeAt(sheet, pieces, state, width, height);

        return new JsonObject
        {
            ["kind"] = "nineSlice",
            ["textureKey"] = TextureKeyOf(asset),
            ["leftWidth"] = ThemeNumbers.Json(leftWidth),
            ["topHeight"] = ThemeNumbers.Json(topHeight),
            ["rightWidth"] = ThemeNumbers.Json(rightWidth),
            ["bottomHeight"] = ThemeNumbers.Json(bottomHeight),
            ["repeat"] = axis,
        };
    }

    /// <summary>
    /// The layers a group of pieces is drawn as, with the sprites they name: one sprite, one
    /// nine-slice, or a composite of every piece placed as the renderer places it, and a tile layer
    /// for each piece that tiles on its own.
    /// </summary>
    private static List<JsonObject> GroupLayer(
        RgbaImage sheet,
        List<SkinPiece> pieces,
        SkinState state,
        string stem,
        Dictionary<string, RgbaImage> sprites,
        bool artSized
    )
    {
        var (width, height) = (state.Width, state.Height);
        var layers = new List<JsonObject>();
        var tiled = pieces.Where(x => x.IsTiled).ToList();
        var rest = pieces.Where(x => !tiled.Contains(x)).ToList();
        var repeating = RepeatingNineSlice(sheet, pieces, state, stem, sprites);

        if (repeating is not null)
            return [repeating];

        // Every entity fixed on both axes is a bitmap of one size, drawn at it whatever the window's
        // size: a sprite where the component takes its size from its art, else one piece at its own
        // size, so a box sized by its layout does not stretch it.
        var fixedSize =
            rest.Count > 0 && rest.All(x => x.H == SkinScale.Fixed && x.V == SkinScale.Fixed);

        if (fixedSize)
        {
            var asset = AssetName(stem);

            sprites[asset] = ComposeSheet(sheet, rest, width, height);
            layers.Add(
                artSized
                    ? Sprite(asset)
                    : new JsonObject
                    {
                        ["kind"] = "composite",
                        ["pieces"] = new JsonArray(
                            new JsonObject
                            {
                                ["textureKey"] = TextureKeyOf(asset),
                                ["left"] = 0,
                                ["top"] = 0,
                                ["width"] = ThemeNumbers.Json(width),
                                ["height"] = ThemeNumbers.Json(height),
                            }
                        ),
                    }
            );
        }
        else if (artSized && rest.Count == 1 && tiled.Count == 0)
        {
            // One piece in a component sized by its art (a bubble's pointer): the box is the piece.
            var asset = AssetName(stem);

            sprites[asset] = CutPiece(sheet, rest[0]);
            layers.Add(Sprite(asset));
        }
        else if (
            rest.Count == 1
            && rest[0].H == SkinScale.Strech
            && rest[0].V == SkinScale.Strech
            && rest[0].At.X == 0
            && rest[0].At.Y == 0
            && rest[0].At.Width == width
            && rest[0].At.Height == height
        )
        {
            var asset = AssetName(stem);

            sprites[asset] = CutPiece(sheet, rest[0]);
            layers.Add(Sprite(asset));
        }
        else if (rest.Count > 0)
        {
            var grid = NineSliceOf(rest, width, height, GridOf(state));
            // A grid whose edges take the whole skin on an axis has no centre to stretch there (a
            // scrollbar track): its pieces place exactly instead.
            var insets =
                grid is not null && grid[0] + grid[2] < width && grid[1] + grid[3] < height
                    ? grid
                    : null;

            if (insets is not null)
            {
                var asset = AssetName(stem);

                sprites[asset] = ComposeSheet(sheet, rest, width, height);
                layers.Add(
                    new JsonObject
                    {
                        ["kind"] = "nineSlice",
                        ["textureKey"] = TextureKeyOf(asset),
                        ["leftWidth"] = ThemeNumbers.Json(insets[0]),
                        ["topHeight"] = ThemeNumbers.Json(insets[1]),
                        ["rightWidth"] = ThemeNumbers.Json(insets[2]),
                        ["bottomHeight"] = ThemeNumbers.Json(insets[3]),
                    }
                );
            }
            else
            {
                layers.Add(Composite(sheet, rest, state, stem, sprites));
            }
        }

        foreach (var piece in tiled)
        {
            var asset = AssetName(stem, piece.Name);
            var at = NaturalRect(piece, width, height);

            sprites[asset] = CutPiece(sheet, piece);

            // A tile over the whole skin fills the box it is drawn in.
            if (at.X == 0 && at.Y == 0 && at.Width == width && at.Height == height)
            {
                layers.Add(
                    new JsonObject { ["kind"] = "tile", ["textureKey"] = TextureKeyOf(asset) }
                );

                continue;
            }

            var layer = new JsonObject { ["kind"] = "tile", ["textureKey"] = TextureKeyOf(asset) };

            layer["left"] = ThemeNumbers.Json(at.X);

            if (piece.H == SkinScale.Tiled)
                layer["right"] = ThemeNumbers.Json(width - (at.X + at.Width));
            else
                layer["width"] = ThemeNumbers.Json(at.Width);

            layer["top"] = ThemeNumbers.Json(at.Y);

            if (piece.V == SkinScale.Tiled)
                layer["bottom"] = ThemeNumbers.Json(height - (at.Y + at.Height));

            layers.Add(layer);
        }

        return layers;
    }

    /// <summary>A composite of every piece, each cut at its own size and placed by its own scale modes.</summary>
    private static JsonObject Composite(
        RgbaImage sheet,
        List<SkinPiece> pieces,
        SkinState state,
        string stem,
        Dictionary<string, RgbaImage> sprites
    )
    {
        var placed = new JsonArray();

        foreach (var piece in pieces)
        {
            var asset = AssetName(stem, piece.Name);

            sprites[asset] = CutPiece(sheet, piece);
            placed.Add(CompositePiece(piece, TextureKeyOf(asset), state.Width, state.Height));
        }

        return new JsonObject { ["kind"] = "composite", ["pieces"] = placed };
    }

    private static JsonObject Sprite(string asset) =>
        new() { ["kind"] = "sprite", ["textureKey"] = TextureKeyOf(asset) };

    /// <summary>
    /// The one layer a slot holds for a group of pieces: the group's own layer where it is one;
    /// otherwise a composite of every piece, never a nine-slice stretched whole into a composite.
    /// </summary>
    private static JsonObject? SlotLayer(
        RgbaImage sheet,
        List<SkinPiece> pieces,
        SkinState state,
        string stem,
        Dictionary<string, RgbaImage> sprites,
        bool artSized
    )
    {
        var layers = GroupLayer(sheet, pieces, state, stem, sprites, artSized);

        return layers.Count <= 1
            ? layers.FirstOrDefault()
            : Composite(sheet, pieces, state, stem, sprites);
    }

    /// <summary>What one state draws: its tinted layer, its untinted body (<c>plain</c>), the rest (<c>overlay</c>), and all its untinted pieces as one layer on request.</summary>
    private sealed record DrawnState(
        JsonObject? Tinted,
        JsonObject? Plain,
        JsonObject? Overlay,
        Func<JsonObject?>? Untinted,
        Dictionary<string, RgbaImage> Sprites
    );

    private static DrawnState StateLayers(
        ThemeLibrary library,
        ThemeSkin skin,
        SkinState state,
        bool artSized
    )
    {
        var sheet = state.Sheet.Length > 0 ? library.Sheet(state.Sheet) : null;
        var sprites = new Dictionary<string, RgbaImage>();

        string Stem(params string[] parts) =>
            string.Join(
                '-',
                new[] { SkinStem(skin), state.Layout ?? "", state.Name }
                    .Concat(parts)
                    .Where(x => x.Length > 0)
            );

        // A skin whose template names no sheet (`habbo_skin_tab_context`) draws nothing of its own.
        if (sheet is null)
            return new DrawnState(null, null, null, null, sprites);

        var hsv = state.Pieces.Where(x => x.Shade is not null).ToList();
        var tinted = state.Pieces.Where(x => x.Colorize && x.Shade is null).ToList();
        var plain = state.Pieces.Where(x => !x.Colorize).ToList();

        JsonObject? tintedLayer = null;

        if (hsv.Count > 0)
        {
            var insets = NineSliceOf(hsv, state.Width, state.Height, GridOf(state)) ?? [0, 0, 0, 0];
            var shades = new JsonArray();

            foreach (var shade in hsv.Select(x => x.Shade!.Value).Distinct())
            {
                var asset = AssetName(
                    Stem($"shade-{ReplaceFirst(ThemeNumbers.Format(shade), '.', '-')}")
                );

                sprites[asset] = ComposeSheet(
                    sheet,
                    [.. hsv.Where(x => x.Shade == shade)],
                    state.Width,
                    state.Height
                );
                shades.Add(
                    new JsonObject
                    {
                        ["textureKey"] = TextureKeyOf(asset),
                        ["shade"] = ThemeNumbers.Json(shade),
                    }
                );
            }

            tintedLayer = new JsonObject
            {
                ["kind"] = "hsvNineSlice",
                ["leftWidth"] = ThemeNumbers.Json(insets[0]),
                ["topHeight"] = ThemeNumbers.Json(insets[1]),
                ["rightWidth"] = ThemeNumbers.Json(insets[2]),
                ["bottomHeight"] = ThemeNumbers.Json(insets[3]),
                ["layers"] = shades,
            };
        }

        // The tinted pieces are one layer. A body with one tile inset over it (a scrollbar lift's
        // grip) is two: the tile goes in the overlay slot, where the state has no untinted pieces.
        JsonObject? tintedTile = null;

        if (tintedLayer is null && tinted.Count > 0)
        {
            var layers = GroupLayer(sheet, tinted, state, Stem(), sprites, artSized);

            if (layers.Count == 2 && (string?)layers[1]["kind"] == "tile" && plain.Count == 0)
                (tintedLayer, tintedTile) = (layers[0], layers[1]);
            else
                tintedLayer = SlotLayer(sheet, tinted, state, Stem(), sprites, artSized);
        }

        // The untinted pieces: one body where they are one grid; split by name prefix where they are
        // several (a frame's pale body and its `shine_*`), the body its own layer and the rest the overlay.
        bool Whole(List<SkinPiece> pieces) =>
            !pieces.Any(x => x.IsTiled)
            && NineSliceOf(pieces, state.Width, state.Height, GridOf(state)) is not null;

        var groups = plain
            .Select(GroupOf)
            .Distinct()
            .Select(group => (Group: group, Pieces: plain.Where(x => GroupOf(x) == group).ToList()))
            .ToList();
        var plainGroups = Whole(plain) || groups.Count < 2 ? [("", plain)] : groups;
        var body = plainGroups.Any(x => x.Group.Length == 0)
            ? plainGroups.First(x => x.Group.Length == 0)
            : plainGroups[0];
        var plainPieces = body.Pieces;
        var overlayPieces = plainGroups
            .Where(x => !ReferenceEquals(x.Pieces, body.Pieces))
            .SelectMany(x => x.Pieces)
            .ToList();

        return new DrawnState(
            tintedLayer,
            plainPieces.Count > 0
                ? SlotLayer(sheet, plainPieces, state, Stem("plain"), sprites, artSized)
                : null,
            overlayPieces.Count > 0
                ? SlotLayer(sheet, overlayPieces, state, Stem("overlay"), sprites, artSized)
                : tintedTile,
            plain.Count > 0
                ? () => SlotLayer(sheet, plain, state, Stem("untinted"), sprites, artSized)
                : null,
            sprites
        );
    }

    private static string ReplaceFirst(string text, char from, char to)
    {
        var at = text.IndexOf(from);

        return at < 0
            ? text
            : string.Concat(text.AsSpan(0, at), to.ToString(), text.AsSpan(at + 1));
    }

    /// <summary><c>Math.max</c>: <c>NaN</c> wins.</summary>
    private static double Max(double a, double b) =>
        double.IsNaN(a) || double.IsNaN(b) ? double.NaN : Math.Max(a, b);

    /// <summary><c>Math.min</c>: <c>NaN</c> wins.</summary>
    private static double Min(double a, double b) =>
        double.IsNaN(a) || double.IsNaN(b) ? double.NaN : Math.Min(a, b);

    [GeneratedRegex(
        @"_?(top|center|mid|middle|bottom|btm)_(left|center|middle|mid|right)\z|_?(top|bottom|btm|left|right|center|middle)\z"
    )]
    private static partial Regex GridToken();

    [GeneratedRegex("[^A-Za-z0-9-]")]
    private static partial Regex NotAssetCharacter();

    [GeneratedRegex("_skin_")]
    private static partial Regex SkinToken();

    [GeneratedRegex(@"^\$([A-Za-z0-9_]+)\z")]
    private static partial Regex SkinVariable();
}
