using FluentAssertions;
using Turbo.Assets.Conversion;
using Turbo.Assets.Swf;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Assets;

/// <summary>
/// The bitmaps a class of a compiled client embeds, read from its ActionScript and symbol table:
/// how the hotel finds the images Habbo's AIR client has under a library class's field names.
/// </summary>
public sealed class SwfEmbeddedImagesTests
{
    [Fact]
    public void A_classes_embedded_bitmaps_are_found_by_its_static_fields()
    {
        var icon = new RgbaImage(13, 18, Pixels(13, 18, 200)).EncodePng();
        var argb = new byte[2 * 2 * 4];

        argb[0] = 255; // opaque black, premultiplied
        var swf = SwfLibrary.Read(
            TestAirClient.Swf(
                (
                    "HabboWindowManagerCom",
                    [
                        TestAirClient.Png(
                            "cursed_dragon_adoption_icon",
                            "icon_png$547e62df365e1b8848caef35dc754d4d-1656250586",
                            icon
                        ),
                        TestAirClient.Argb("frame_close", "close_png$1-2", 2, 2, argb),
                        // An embedded layout is no bitmap.
                        TestAirClient.Binary("purse_xml", "purse_xml$3-4", "<layout/>"),
                    ]
                ),
                ("HabboToolbarCom", [TestAirClient.Png("coin_icon", "coin_png$5-6", icon)])
            )
        );

        var found = swf.EmbeddedImages(x => x == "HabboWindowManagerCom");

        found.Keys.Should().Equal("HabboWindowManagerCom");
        found["HabboWindowManagerCom"]
            .Keys.Should()
            .BeEquivalentTo("cursed_dragon_adoption_icon", "frame_close");

        var decoded = SwfImageDecoder.Decode(
            found["HabboWindowManagerCom"]["cursed_dragon_adoption_icon"]
        );

        decoded.Width.Should().Be(13);
        decoded.Height.Should().Be(18);
        decoded.Pixels.Should().Equal(Pixels(13, 18, 200));
        SwfImageDecoder
            .Decode(found["HabboWindowManagerCom"]["frame_close"])
            .Alpha(0, 0)
            .Should()
            .Be(255);
    }

    [Fact]
    public void A_classes_embedded_layouts_are_found_by_its_static_fields()
    {
        var swf = SwfLibrary.Read(
            TestAirClient.Swf(
                (
                    "HabboToolbarCom",
                    [
                        TestAirClient.Binary(
                            "prog_menu_view_xml",
                            "HabboToolbarCom_prog_menu_view_xml",
                            "<layout/>"
                        ),
                        // A bitmap is no file.
                        TestAirClient.Png(
                            "coin_icon",
                            "coin_png$5-6",
                            new RgbaImage(1, 1, Pixels(1, 1, 1)).EncodePng()
                        ),
                    ]
                ),
                (
                    "HabboWindowManagerCom",
                    [TestAirClient.Binary("purse_xml", "purse_xml$3-4", "<purse/>")]
                )
            )
        );

        var found = swf.EmbeddedBinaries(x => x == "HabboToolbarCom");

        found.Keys.Should().Equal("HabboToolbarCom");
        found["HabboToolbarCom"].Keys.Should().Equal("prog_menu_view_xml");
        System
            .Text.Encoding.UTF8.GetString(found["HabboToolbarCom"]["prog_menu_view_xml"])
            .Should()
            .Be("<layout/>");
    }

    [Fact]
    public void An_swf_without_actionscript_embeds_nothing()
    {
        SwfLibrary.Read(NitroConverterTests.Swf()).EmbeddedImages(_ => true).Should().BeEmpty();
    }

    private static byte[] Pixels(int width, int height, byte red)
    {
        var pixels = new byte[width * height * 4];

        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = red;
            pixels[i + 3] = 255;
        }

        return pixels;
    }
}
