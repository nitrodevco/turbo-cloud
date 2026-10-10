using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using FluentAssertions;
using Turbo.Assets.Conversion;
using Turbo.Assets.Nitro;
using Turbo.Assets.Swf;
using Xunit;

namespace Turbo.Tests.Assets;

/// <summary>
/// Libraries converted to <c>.nitro</c> bundles as the studio's converter made them. Its output on
/// Habbo's own furniture, pets and clothing was compared with this one's when it was ported; these
/// keep the rules that comparison rests on.
/// </summary>
public sealed class NitroConverterTests
{
    private const string LIB = "test_box";

    [Fact]
    public void an_swf_becomes_asset_data_a_sheet_and_its_frames()
    {
        var bundle = NitroConverter.Convert(Swf());

        bundle.Files.Keys.Should().Equal($"{LIB}.json", $"{LIB}.png", $"{LIB}_spritesheet.json");

        var data = JsonNode.Parse(bundle.Files[$"{LIB}.json"])!.AsObject();

        data.Select(x => x.Key)
            .Should()
            .Equal("type", "logicType", "visualizationType", "assets", "logic", "visualizations");
        // The 32 size is never drawn; the 64 asset is, its alias resolved to the image it shares.
        data["visualizations"]!
            .AsArray()
            .Select(x => x!["size"]!.GetValue<int>())
            .Should()
            .Equal(64);
        data["assets"]!
            .AsArray()
            .Select(x => x!["name"]!.GetValue<string>())
            .Should()
            .Equal($"{LIB}_64_a_0_0", $"{LIB}_64_b_0_0", $"{LIB}_icon_a");
        data["assets"]![1]!["source"]!.GetValue<string>().Should().Be($"{LIB}_64_a_0_0");
        data["logic"]!["model"]!["dimensions"]!
            .ToJsonString()
            .Should()
            .Be("""{"x":1,"y":1,"z":0.000001}""");

        var frames = JsonNode.Parse(bundle.Files[$"{LIB}_spritesheet.json"])!["frames"]!.AsObject();

        frames.Select(x => x.Key).Should().BeEquivalentTo($"{LIB}_64_a_0_0", $"{LIB}_icon_a");

        // Trimmed of its transparent edge: a 4x4 image with a 2x2 middle.
        var frame = frames[$"{LIB}_64_a_0_0"]!;

        frame["trimmed"]!.GetValue<bool>().Should().BeTrue();
        frame["spriteSourceSize"]!.ToJsonString().Should().Be("""{"x":1,"y":1,"w":2,"h":2}""");
        frame["sourceSize"]!.ToJsonString().Should().Be("""{"w":4,"h":4}""");

        // An image with nothing in it is left whole, as free-tex-packer leaves it.
        frames[$"{LIB}_icon_a"]!["trimmed"]!.GetValue<bool>().Should().BeFalse();

        var sheet = RgbaImage.Decode(bundle.Files[$"{LIB}.png"]);
        var x = frame["frame"]!["x"]!.GetValue<int>();
        var y = frame["frame"]!["y"]!.GetValue<int>();

        // Unpremultiplied: half-transparent red stored as (128, 0, 0, 128) reads back as 255.
        sheet
            .Pixels.AsSpan(((y * sheet.Width) + x) * 4, 4)
            .ToArray()
            .Should()
            .Equal(255, 0, 0, 128);
    }

    [Fact]
    public void images_with_the_same_pixels_share_one_place()
    {
        var bundle = NitroConverter.Convert(Swf(twoAlike: true));
        var frames = JsonNode.Parse(bundle.Files[$"{LIB}_spritesheet.json"])!["frames"]!.AsObject();

        frames[$"{LIB}_64_a_0_0"]!["frame"]!
            .ToJsonString()
            .Should()
            .Be(frames[$"{LIB}_64_c_0_0"]!["frame"]!.ToJsonString());
    }

    [Fact]
    public void a_bundle_writes_and_reads_back_the_same()
    {
        var bundle = NitroConverter.Convert(Swf());
        var written = bundle.Write();
        var read = NitroBundle.Read(written);

        read.Files.Keys.Should().Equal(bundle.Files.Keys);
        read.Files[$"{LIB}.json"].Should().Equal(bundle.Files[$"{LIB}.json"]);
        // The same files zip to the same bytes.
        NitroConverter.Convert(Swf()).Write().Should().Equal(written);
    }

    [Fact]
    public void a_prebuilt_hab_keeps_its_sheet_and_comes_to_the_current_layout()
    {
        var legacy = new JsonObject
        {
            ["name"] = "TileCursor",
            ["type"] = "tile_cursor",
            ["documentClass"] = "TileCursor",
            ["logicType"] = "tile_cursor",
            ["assets"] = new JsonObject
            {
                ["tile_cursor_64_a_0_0"] = new JsonObject { ["x"] = 1, ["y"] = 2 },
            },
            ["visualizations"] = new JsonArray(
                new JsonObject
                {
                    ["size"] = 64,
                    ["layers"] = new JsonObject
                    {
                        ["10"] = new JsonObject { ["z"] = 1 },
                        ["2"] = new JsonObject { ["z"] = 2 },
                    },
                }
            ),
            ["spritesheet"] = new JsonObject
            {
                ["frames"] = new JsonObject
                {
                    ["TileCursor_tile_cursor_64_a_0_0"] = new JsonObject
                    {
                        ["frame"] = new JsonObject(),
                    },
                },
                ["meta"] = new JsonObject { ["image"] = "old.png" },
            },
        };

        var bundle = NitroConverter.Convert(
            Hab(
                (
                    "TileCursor.json",
                    "application/json",
                    Encoding.UTF8.GetBytes(legacy.ToJsonString())
                ),
                ("TileCursor.png", "image/png", [1, 2, 3])
            )
        );
        var data = JsonNode.Parse(bundle.Files["TileCursor.json"])!.AsObject();

        data.ToJsonString()
            .Should()
            .Be(
                """{"type":"tile_cursor","logicType":"tile_cursor","assets":[{"name":"tile_cursor_64_a_0_0","x":1,"y":2}],"visualizations":[{"size":64,"layers":[{"id":2,"z":2},{"id":10,"z":1}]}]}"""
            );
        bundle.Files["TileCursor.png"].Should().Equal(1, 2, 3);
        JsonNode
            .Parse(bundle.Files["TileCursor_spritesheet.json"])!
            .ToJsonString()
            .Should()
            .Be(
                """{"frames":{"tile_cursor_64_a_0_0":{"frame":{}}},"meta":{"image":"TileCursor.png"}}"""
            );
    }

    [Theory]
    [InlineData("64a", 64d)]
    [InlineData(" -12.9", -12d)]
    [InlineData("0x1F", 31d)]
    [InlineData("abc", null)]
    public void numbers_are_read_as_javascript_reads_them(string text, double? expected) =>
        JsValues.ParseInt(text).Should().Be(expected);

    [Theory]
    [InlineData(0.000001, "0.000001")]
    [InlineData(0.0000001, "1e-7")]
    [InlineData(1.1125, "1.1125")]
    [InlineData(1e21, "1e+21")]
    [InlineData(123456789012345678d, "123456789012345680")]
    [InlineData(-0d, "0")]
    public void numbers_are_written_as_javascript_writes_them(double value, string expected) =>
        JsValues.Number(value)!.ToJsonString().Should().Be(expected);

    // ------------------------------------------------------------------ files

    private const string INDEX =
        """<object type="test_box" visualization="furniture_static" logic="furniture_basic"/>""";

    private const string ASSETS = """
        <assets>
          <asset name="test_box_64_a_0_0" x="10" y="20"/>
          <asset name="test_box_64_b_0_0" x="10" y="20"/>
          <asset name="test_box_32_a_0_0" x="5" y="10"/>
          <asset name="test_box_icon_a" x="1" y="1"/>
        </assets>
        """;

    private const string LOGIC =
        """<objectData type="test_box"><model><dimensions x="1" y="1" z="0.000001"/><directions><direction id="90"/></directions></model></objectData>""";

    private const string VISUALIZATION = """
        <visualizationData type="test_box"><graphics>
          <visualization size="32" layerCount="1" angle="45"/>
          <visualization size="64" layerCount="1" angle="45"/>
        </graphics></visualizationData>
        """;

    /// <summary>
    /// An SWF with a 4x4 image whose red 2x2 middle is half transparent, exported as <c>_64_a_0_0</c>
    /// and again as <c>_64_b_0_0</c>; an empty icon; and - when <paramref name="twoAlike"/> - the
    /// same picture as a separate image, <c>_64_c_0_0</c>, with an asset of its own.
    /// </summary>
    private static byte[] Swf(bool twoAlike = false)
    {
        var assets = twoAlike
            ? ASSETS.Replace(
                "</assets>",
                """<asset name="test_box_64_c_0_0" x="0" y="0"/></assets>""",
                StringComparison.Ordinal
            )
            : ASSETS;
        var tags = new MemoryStream();
        var symbols = new List<(int Id, string Name)> { (0, LIB) };
        var id = 1;

        foreach (
            var (name, xml) in new[]
            {
                ("index", INDEX),
                ($"{LIB}_assets", assets),
                ($"{LIB}_logic", LOGIC),
                ($"{LIB}_visualization", VISUALIZATION),
            }
        )
        {
            var data = new MemoryStream();

            Write16(data, id);
            data.Write(new byte[4]);
            data.Write(Encoding.UTF8.GetBytes(xml));
            Tag(tags, SwfLibrary.TAG_DEFINE_BINARY_DATA, data.ToArray());
            symbols.Add((id++, $"{LIB}_{name}"));
        }

        var box = new byte[4 * 4 * 4];

        foreach (var (x, y) in new[] { (1, 1), (2, 1), (1, 2), (2, 2) })
        {
            var p = ((y * 4) + x) * 4;

            // Premultiplied ARGB: half-transparent red.
            box[p] = 128;
            box[p + 1] = 128;
        }

        var boxId = id++;

        Tag(tags, SwfLibrary.TAG_DEFINE_BITS_LOSSLESS2, Lossless(boxId, 4, 4, box));
        symbols.Add((boxId, $"{LIB}_{LIB}_64_a_0_0"));
        symbols.Add((boxId, $"{LIB}_{LIB}_64_b_0_0"));

        var iconId = id++;

        Tag(tags, SwfLibrary.TAG_DEFINE_BITS_LOSSLESS2, Lossless(iconId, 2, 2, new byte[16]));
        symbols.Add((iconId, $"{LIB}_{LIB}_icon_a"));

        if (twoAlike)
        {
            var copyId = id++;

            Tag(tags, SwfLibrary.TAG_DEFINE_BITS_LOSSLESS2, Lossless(copyId, 4, 4, box));
            symbols.Add((copyId, $"{LIB}_{LIB}_64_c_0_0"));
        }

        var symbolTag = new MemoryStream();

        Write16(symbolTag, symbols.Count);

        foreach (var (symbolId, name) in symbols)
        {
            Write16(symbolTag, symbolId);
            symbolTag.Write(Encoding.UTF8.GetBytes(name + "\0"));
        }

        Tag(tags, SwfLibrary.TAG_SYMBOL_CLASS, symbolTag.ToArray());
        Tag(tags, SwfLibrary.TAG_END, []);

        return Body(tags.ToArray());
    }

    private static byte[] Body(byte[] tags)
    {
        var file = new MemoryStream();

        file.Write("FWS"u8);
        file.WriteByte(10);
        Write32(file, tags.Length + 13);
        file.Write([0x00, 0x00, 0x18, 0x01, 0x00]);
        file.Write(tags);

        return file.ToArray();
    }

    private static byte[] Lossless(int id, int width, int height, byte[] argb)
    {
        var data = new MemoryStream();
        var packed = new MemoryStream();

        using (var zlib = new ZLibStream(packed, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(argb);

        Write16(data, id);
        data.WriteByte(5);
        Write16(data, width);
        Write16(data, height);
        data.Write(packed.ToArray());

        return data.ToArray();
    }

    private static byte[] Hab(params (string Name, string Mime, byte[] Data)[] files)
    {
        var payload = new MemoryStream();
        var entries = new JsonArray();

        foreach (var (name, mime, data) in files)
        {
            entries.Add(
                new JsonObject
                {
                    ["name"] = name,
                    ["mimeType"] = mime,
                    ["offset"] = payload.Length,
                    ["storedLength"] = data.Length,
                    ["originalLength"] = data.Length,
                    ["compression"] = "none",
                }
            );
            payload.Write(data);
        }

        var manifest = Encoding.UTF8.GetBytes(
            new JsonObject
            {
                ["format"] = "hab",
                ["version"] = 1,
                ["name"] = "TileCursor",
                ["entries"] = entries,
            }.ToJsonString()
        );
        var stored = new MemoryStream();

        using (var zlib = new ZLibStream(stored, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(manifest);

        var file = new MemoryStream();

        file.Write("HAB\0"u8);
        Write16(file, 1);
        Write16(file, 1);
        Write32(file, (int)stored.Length);
        Write32(file, manifest.Length);
        Write32(file, (int)payload.Length);
        file.Write(stored.ToArray());
        file.Write(payload.ToArray());

        return file.ToArray();
    }

    private static void Tag(Stream output, int code, byte[] data)
    {
        Write16(output, (code << 6) | 0x3f);
        Write32(output, data.Length);
        output.Write(data);
    }

    private static void Write16(Stream output, int value)
    {
        Span<byte> bytes = stackalloc byte[2];

        BinaryPrimitives.WriteUInt16LittleEndian(bytes, (ushort)value);
        output.Write(bytes);
    }

    private static void Write32(Stream output, int value)
    {
        Span<byte> bytes = stackalloc byte[4];

        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        output.Write(bytes);
    }
}
