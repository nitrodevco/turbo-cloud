using System.Text;
using FluentAssertions;
using Turbo.Assets.Conversion;
using Turbo.Assets.Nitro;
using Xunit;

namespace Turbo.Tests.Assets;

/// <summary>
/// A furniture's icon drawn as nitro-studio draws it. Its whole drawing was checked against the
/// Studio's on a sample of Habbo's furniture, every pixel alike; these hold what a change may break.
/// </summary>
public sealed class FurnitureIconTests
{
    [Fact]
    public void A_group_furniture_draws_its_colour_layers_in_a_groups_colours_and_the_rest_as_they_are()
    {
        var icon = FurnitureIcon.Draw(Flag(), "flag")!;

        // Layer 0 has no tag: untinted. COLOR1 is a group's first colour, EEEEEE; the badge is not drawn.
        Pixel(icon, 0, 0).Should().Be(0xFFFFFFFF);
        Pixel(icon, 1, 0).Should().Be(0xEEEEEEFF);
        icon.Width.Should().Be(2);
    }

    [Fact]
    public void A_mirrored_layer_spans_to_the_left_of_its_point_and_a_bundle_with_no_icon_draws_none()
    {
        var icon = FurnitureIcon.Draw(Mirrored(), "sign")!;

        // Layer a at -1,0; layer b mirrored at x 1 spans 1 - 2 = -1 .. 1, drawn over it.
        (icon.Width, icon.Height)
            .Should()
            .Be((2, 1));
        Pixel(icon, 0, 0).Should().Be(0x0000FFFF);
        Pixel(icon, 1, 0).Should().Be(0xFF0000FF);

        FurnitureIcon
            .Draw(
                NitroBundle.Create(
                    new Dictionary<string, byte[]> { ["x.json"] = "{}"u8.ToArray() }
                ),
                "x"
            )
            .Should()
            .BeNull();
    }

    private static uint Pixel(RgbaImage image, int x, int y)
    {
        var at = ((y * image.Width) + x) * 4;

        return (uint)(
            (image.Pixels[at] << 24)
            | (image.Pixels[at + 1] << 16)
            | (image.Pixels[at + 2] << 8)
            | image.Pixels[at + 3]
        );
    }

    /// <summary>One pixel each: white, white, white; a group flag's plain, COLOR1 and BADGE layers.</summary>
    private static NitroBundle Flag() =>
        Bundle(
            "flag",
            """
            {"type":"flag","visualizationType":"furniture_guild_customized",
             "visualizations":[{"size":1,"layerCount":3,"layers":[{"id":0},{"id":1,"tag":"COLOR1"},{"id":2,"tag":"BADGE"}]}],
             "assets":[{"name":"flag_icon_a","x":0,"y":0},{"name":"flag_icon_b","x":-1,"y":0},{"name":"flag_icon_c","x":-2,"y":0}]}
            """,
            ["flag_icon_a", "flag_icon_b", "flag_icon_c"],
            [0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF]
        );

    /// <summary>Two pixels: red at its point, and a two pixel layer mirrored over it.</summary>
    private static NitroBundle Mirrored() =>
        Bundle(
            "sign",
            """
            {"type":"sign","visualizations":[{"size":1,"layerCount":2}],
             "assets":[{"name":"sign_icon_a","x":0,"y":0},{"name":"sign_icon_b","x":1,"y":0,"flipH":true}]}
            """,
            ["sign_icon_a", "sign_icon_b"],
            [0xFF0000FF, 0xFF0000FF, 0x0000FFFF],
            widths: [1, 2]
        );

    /// <summary>A bundle whose sheet is a row of pixels, each frame the next <paramref name="widths"/> of them.</summary>
    private static NitroBundle Bundle(
        string name,
        string json,
        string[] frames,
        uint[] row,
        int[]? widths = null
    )
    {
        widths ??= [.. frames.Select(_ => 1)];

        var pixels = new byte[row.Length * 4];

        for (var x = 0; x < row.Length; x++)
        {
            pixels[(x * 4) + 0] = (byte)(row[x] >> 24);
            pixels[(x * 4) + 1] = (byte)(row[x] >> 16);
            pixels[(x * 4) + 2] = (byte)(row[x] >> 8);
            pixels[(x * 4) + 3] = (byte)row[x];
        }

        var at = 0;
        var sheet = string.Join(
            ",",
            frames.Select(
                (frame, i) =>
                {
                    var entry = $$$"""
                    "{{{frame}}}":{"frame":{"x":{{{at}}},"y":0,"w":{{{widths[i]}}},"h":1}}
                    """;

                    at += widths[i];

                    return entry;
                }
            )
        );

        return NitroBundle.Create(
            new Dictionary<string, byte[]>
            {
                [$"{name}.json"] = Encoding.UTF8.GetBytes(json),
                [$"{name}_spritesheet.json"] = Encoding.UTF8.GetBytes(
                    $$$"""{"frames":{{{{sheet}}}},"meta":{"image":"{{{name}}}.png"}}"""
                ),
                [$"{name}.png"] = new RgbaImage(row.Length, 1, pixels).EncodePng(),
            }
        );
    }
}
