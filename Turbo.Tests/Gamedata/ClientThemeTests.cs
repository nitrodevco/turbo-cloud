using System.Text;
using System.Text.Json.Nodes;
using FluentAssertions;
using Turbo.Assets.Conversion;
using Turbo.Assets.Hab;
using Turbo.Gamedata.Habbo.Client.Theme;
using Turbo.Tests.Assets;
using Xunit;

namespace Turbo.Tests.Gamedata;

/// <summary>
/// The UI theme built from a window manager library as Nitro Studio builds it: a hand-made library
/// with one themed element (a border, style 0) drawn by a nine-slice skin, and an icon set. What is
/// checked is what nitro-react reads out of the bundle: the variant and cascade in
/// <c>theme-variants.json</c>, each sprite's pixels at its frame in <c>theme.png</c> with its edge
/// extruded into the gutter round it, and the asset names the bundle lists.
/// </summary>
public sealed class ClientThemeTests
{
    private const string XML = "text/xml";
    private const string PNG = "image/png";

    /// <summary>The nine-slice's sheet: three by three pixels, each its own colour.</summary>
    private static readonly RgbaImage BORDER_SHEET = Image(3, 3, (x, y) =>
        [(byte)(40 + (x * 70)), (byte)(30 + (y * 80)), (byte)(10 + (x * 20) + (y * 5)), 255]
    );

    /// <summary>The icon set's sheet: two icons of two by two pixels side by side.</summary>
    private static readonly RgbaImage ICON_SHEET = Image(4, 2, (x, y) =>
        [(byte)(x * 60), (byte)(y * 120), 200, (byte)(100 + (x * 30))]
    );

    /// <summary>A grid cell's entity name, scale on each axis, and place in the three by three skin.</summary>
    private static readonly (string Name, string H, string V, int X, int Y)[] CELLS =
    [
        ("top_left", "fixed", "fixed", 0, 0),
        ("top", "strech", "fixed", 1, 0),
        ("top_right", "move", "fixed", 2, 0),
        ("left", "fixed", "strech", 0, 1),
        ("center", "strech", "strech", 1, 1),
        ("right", "move", "strech", 2, 1),
        ("bottom_left", "fixed", "move", 0, 2),
        ("bottom", "strech", "move", 1, 2),
        ("bottom_right", "move", "move", 2, 2),
    ];

    private const string BORDER_ASSET = "theme-habbo-border-border-default";

    [Fact]
    public void The_variants_say_what_the_element_row_its_layout_and_its_skin_say()
    {
        var theme = ClientTheme.Build(Library())!;
        var file = JsonNode.Parse(theme.Files[ClientTheme.VARIANTS])!;

        JsonNode
            .DeepEquals(
                file["variants"]!["border"]!["0"],
                JsonNode.Parse(
                    """
                    {
                        "tintColor": "#418db0",
                        "textStyle": "b_title",
                        "textColor": "#112233",
                        "layout": { "minWidth": 40, "minHeight": 30, "paddingLeft": 3, "paddingTop": 4, "paddingRight": 5, "paddingBottom": 6 },
                        "layer": { "kind": "nineSlice", "textureKey": "habbo-border-border-default-src", "leftWidth": 1, "topHeight": 1, "rightWidth": 1, "bottomHeight": 1 }
                    }
                    """
                )
            )
            .Should()
            .BeTrue(file["variants"]!["border"]!["0"]!.ToJsonString());

        // The rules name style 9 too, with a colour of their own; the library describes no such row.
        file["variants"]!["border"]!["9"]!
            .ToJsonString()
            .Should()
            .Be("""{"tintColor":"#686661"}""");
        // The layout places a border at its own style: what style 0 cascades to that child.
        file["cascade"]!["border"]!["0"]!.ToJsonString().Should().Be("""{"border":"0"}""");
        file["icons"]!
            .ToJsonString()
            .Should()
            .Be(
                """{"1":{"x":0,"y":0,"width":2,"height":2},"2":{"x":2,"y":0,"width":2,"height":2}}"""
            );
        file["version"]!.GetValue<int>().Should().Be(1);
        file["textures"]!.ToJsonString().Should().Be("{}");
    }

    [Fact]
    public void Each_sprite_is_on_the_sheet_with_its_own_edge_round_it_under_the_names_the_bundle_lists()
    {
        var theme = ClientTheme.Build(Library())!;
        var sheet = RgbaImage.Decode(theme.Files[ClientTheme.SHEET]);
        var frames = JsonNode.Parse(theme.Files[ClientTheme.SPRITESHEET])!["frames"]!.AsObject();

        theme.AssetNames.Should().Equal(BORDER_ASSET, "theme-icon-set");
        frames.Select(x => x.Key).Order(StringComparer.Ordinal).Should().Equal(theme.AssetNames);

        foreach (
            var (name, source) in new[]
            {
                (BORDER_ASSET, BORDER_SHEET),
                ("theme-icon-set", ICON_SHEET),
            }
        )
        {
            var frame = frames[name]!["frame"]!;
            var (x, y) = (frame["x"]!.GetValue<int>(), frame["y"]!.GetValue<int>());

            (frame["w"]!.GetValue<int>(), frame["h"]!.GetValue<int>())
                .Should()
                .Be((source.Width, source.Height));
            sheet
                .Crop(x, y, source.Width, source.Height, source.Width, source.Height)
                .SameAs(source)
                .Should()
                .BeTrue($"{name} is drawn as the library has it");

            // One pixel out on every side is the sprite's own nearest edge pixel, corners included.
            var gutter = sheet.Crop(
                x - 1,
                y - 1,
                source.Width + 2,
                source.Height + 2,
                source.Width + 2,
                source.Height + 2
            );
            var extruded = Image(source.Width + 2, source.Height + 2, (gx, gy) =>
                Pixel(
                    source,
                    Math.Clamp(gx - 1, 0, source.Width - 1),
                    Math.Clamp(gy - 1, 0, source.Height - 1)
                )
            );

            gutter.SameAs(extruded).Should().BeTrue($"{name}'s edge is extruded into its gutter");
        }
    }

    [Fact]
    public void A_library_with_no_element_description_cannot_be_read()
    {
        var hab = HabLibrary.Read(
            NitroConverterTests.Hab(("habbo_skin_border_xml", XML, Encoding.UTF8.GetBytes(Skin())))
        );

        var build = () => ClientTheme.Build(hab);

        build.Should().Throw<FormatException>().WithMessage("*habbo_element_description_xml*");
    }

    [Fact]
    public void A_library_whose_elements_draw_nothing_has_no_theme()
    {
        var hab = HabLibrary.Read(
            NitroConverterTests.Hab(
                (
                    "habbo_element_description_xml",
                    XML,
                    Encoding.UTF8.GetBytes(
                        """<elements><window type="border" style="0" /></elements>"""
                    )
                )
            )
        );

        ClientTheme.Build(hab).Should().BeNull();
    }

    /// <summary>
    /// Illumina's multi middle button (container button 106): a face that takes the tint, and one
    /// untinted 1px etch line along its bottom. A stateful component is sized by its art, which
    /// once made that lone untinted piece a sprite, and a sprite fills the button: the etch's pixel
    /// covered the face. It is a nine-slice with the line in its bottom row, as nitro-theme's
    /// <c>skinGroupLayers</c> places it.
    /// </summary>
    [Fact]
    public void A_lone_untinted_strip_of_a_button_face_is_placed_along_its_edge_not_stretched_over_it()
    {
        var sheet = Image(1, 4, (_, y) => [(byte)(60 * y), 120, 200, 255]);
        var hab = NitroConverterTests.Hab(
            (
                "habbo_element_description_xml",
                XML,
                Encoding.UTF8.GetBytes(
                    """
                    <elements>
                        <window type="container_button" style="106" renderer="skin" asset="illumina_light_skin_button_multi_middle_xml" layout="multi_middle" />
                    </elements>
                    """
                )
            ),
            (
                "illumina_light_skin_button_multi_middle_xml",
                XML,
                Encoding.UTF8.GetBytes(
                    """
                    <skin>
                        <templates>
                            <template name="face" asset="illumina_light_button_multi">
                                <entities>
                                    <entity name="button_top_center"><region><Rectangle x="0" y="0" width="1" height="1" /></region></entity>
                                    <entity name="button_center_center"><region><Rectangle x="0" y="1" width="1" height="1" /></region></entity>
                                    <entity name="button_bottom_center"><region><Rectangle x="0" y="2" width="1" height="1" /></region></entity>
                                    <entity name="button_etch_center"><region><Rectangle x="0" y="3" width="1" height="1" /></region></entity>
                                </entities>
                            </template>
                        </templates>
                        <layouts>
                            <layout name="multi_middle">
                                <entities>
                                    <entity name="button_top_center" colorize="true"><region><Rectangle x="0" y="0" width="11" height="4" /></region><scale horizontal="strech" vertical="fixed" /></entity>
                                    <entity name="button_center_center" colorize="true"><region><Rectangle x="0" y="4" width="11" height="10" /></region><scale horizontal="strech" vertical="strech" /></entity>
                                    <entity name="button_bottom_center" colorize="true"><region><Rectangle x="0" y="14" width="11" height="4" /></region><scale horizontal="strech" vertical="move" /></entity>
                                    <entity name="button_etch_center" colorize="false"><region><Rectangle x="0" y="18" width="11" height="1" /></region><scale horizontal="strech" vertical="move" /></entity>
                                </entities>
                            </layout>
                        </layouts>
                        <states><state name="default" template="face" layout="multi_middle" /></states>
                    </skin>
                    """
                )
            ),
            ("illumina_light_button_multi_png", PNG, sheet.EncodePng())
        );

        var theme = ClientTheme.Build(HabLibrary.Read(hab))!;
        var variant = JsonNode.Parse(theme.Files[ClientTheme.VARIANTS])!["variants"]![
            "containerButton"
        ]!["106"]!;

        variant["overlays"]!["default"]!["kind"]!.GetValue<string>().Should().Be("nineSlice");
        variant["overlays"]!["default"]!
            .ToJsonString()
            .Should()
            .Contain(
                """
                "leftWidth":0,"topHeight":4,"rightWidth":0,"bottomHeight":5
                """
            );
        variant["states"]!["default"]!["kind"]!.GetValue<string>().Should().Be("nineSlice");
    }

    /// <summary>The window manager library: the element row, its window layout, its skin and sheet, and the icon set.</summary>
    private static HabLibrary Library() => HabLibrary.Read(WindowManagerHab());

    /// <summary>The window manager library's .hab, as a release carries it.</summary>
    internal static byte[] WindowManagerHab() =>
        NitroConverterTests.Hab(
            (
                "habbo_element_description_xml",
                XML,
                Encoding.UTF8.GetBytes(
                    """
                    <elements>
                        <window type="border" style="0" renderer="skin" asset="habbo_skin_border_xml" window_layout="habbo_window_layout_border_xml" layout="border" color="0xff418db0" />
                    </elements>
                    """
                )
            ),
            (
                "habbo_window_layout_border_xml",
                XML,
                Encoding.UTF8.GetBytes(
                    """
                    <layout width="40" height="30">
                        <window>
                            <border style="0" x="0" y="0" width="40" height="30">
                                <children>
                                    <label name="caption" tags="title, _EXCLUDE">
                                        <variables>
                                            <var key="text_style" value="b_title" />
                                            <var key="text_color" value="0xff112233" />
                                            <var key="margins">
                                                <value>
                                                    <Map>
                                                        <var key="left" value="3" />
                                                        <var key="top" value="4" />
                                                        <var key="right" value="5" />
                                                        <var key="bottom" value="6" />
                                                    </Map>
                                                </value>
                                            </var>
                                        </variables>
                                    </label>
                                </children>
                            </border>
                        </window>
                    </layout>
                    """
                )
            ),
            ("habbo_skin_border_xml", XML, Encoding.UTF8.GetBytes(Skin())),
            // The skin names its sheet without `_png`, as the client's do.
            ("habbo_border_png", PNG, BORDER_SHEET.EncodePng()),
            (
                "habbo_skin_icon_set_xml",
                XML,
                Encoding.UTF8.GetBytes(
                    """
                    <skin>
                        <variables><variable key="sheet" value="habbo_icons" /></variables>
                        <templates>
                            <template name="icon_1" asset="$sheet"><entities><entity name="icon"><region><Rectangle x="0" y="0" width="2" height="2" /></region></entity></entities></template>
                            <template name="icon_2" asset="$sheet"><entities><entity name="icon"><region><Rectangle x="2" y="0" width="2" height="2" /></region></entity></entities></template>
                        </templates>
                    </skin>
                    """
                )
            ),
            ("habbo_icons_png", PNG, ICON_SHEET.EncodePng())
        );

    /// <summary>A three by three nine-slice skin: each pixel of the sheet one cell, fixed, stretched or moving as its place says.</summary>
    private static string Skin()
    {
        static string Rect(int x, int y) =>
            $"""<region><Rectangle x="{x}" y="{y}" width="1" height="1" /></region>""";

        var regions = string.Concat(
            CELLS.Select(x => $"""<entity name="{x.Name}">{Rect(x.X, x.Y)}</entity>""")
        );
        var placed = string.Concat(
            CELLS.Select(x =>
                $"""<entity name="{x.Name}">{Rect(x.X, x.Y)}<scale horizontal="{x.H}" vertical="{x.V}" /></entity>"""
            )
        );

        return $"""
            <skin>
                <templates><template name="border" asset="habbo_border"><entities>{regions}</entities></template></templates>
                <layouts><layout name="border"><entities>{placed}</entities></layout></layouts>
                <states><state name="default" template="border" layout="border" /></states>
            </skin>
            """;
    }

    private static RgbaImage Image(int width, int height, Func<int, int, byte[]> pixel)
    {
        var pixels = new byte[width * height * 4];

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            pixel(x, y).CopyTo(pixels, ((y * width) + x) * 4);

        return new RgbaImage(width, height, pixels);
    }

    private static byte[] Pixel(RgbaImage image, int x, int y) =>
        image.Pixels.AsSpan(((y * image.Width) + x) * 4, 4).ToArray();
}
