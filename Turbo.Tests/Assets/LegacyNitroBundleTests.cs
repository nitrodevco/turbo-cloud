using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using FluentAssertions;
using Turbo.Assets;
using Turbo.Assets.Conversion;
using Turbo.Assets.Furniture;
using Turbo.Assets.Nitro;
using Xunit;

namespace Turbo.Tests.Assets;

/// <summary>
/// A <c>.nitro</c> of the old layout - a count, then each file's name and zlib'd (or gzip'd) data, its asset
/// data keyed by name and id with the spritesheet inside - is told apart by its layout, read, and
/// repacked as the zip the client reads.
/// </summary>
public sealed class LegacyNitroBundleTests
{
    private const string NAME = "old_chest";

    // Any bytes: the image is carried over as it is.
    private static readonly byte[] IMAGE = "an image"u8.ToArray();

    [Fact]
    public void An_old_bundle_is_told_apart_by_its_layout_to_its_last_byte()
    {
        var legacy = Legacy();

        LegacyNitroBundle.IsLegacy(legacy).Should().BeTrue();

        LegacyNitroBundle.IsLegacy([]).Should().BeFalse();
        LegacyNitroBundle.IsLegacy(legacy[..^1]).Should().BeFalse();
        LegacyNitroBundle.IsLegacy([.. legacy, 0]).Should().BeFalse();
        LegacyNitroBundle.IsLegacy("""{ "type": "chair" }"""u8).Should().BeFalse();
        LegacyNitroBundle
            .IsLegacy(
                NitroBundle.Create(new Dictionary<string, byte[]> { ["a.json"] = [] }).Write()
            )
            .Should()
            .BeFalse();
        LegacyNitroBundle.IsLegacy(NitroConverterTests.Swf()).Should().BeFalse();

        // Its files' data is zlib'd: a file of plain bytes is not one.
        LegacyNitroBundle
            .IsLegacy(
                Pack(new Dictionary<string, byte[]> { ["a.json"] = "{}"u8.ToArray() }, raw => raw)
            )
            .Should()
            .BeFalse();

        LegacyNitroBundle.Read(legacy).Keys.Should().Equal($"{NAME}.json", $"{NAME}.png");
    }

    [Fact]
    public void An_old_bundle_converts_to_the_zip_the_client_reads()
    {
        var bundle = NitroBundle.Read(NitroConverter.Convert(Legacy(), NAME).Write());

        bundle
            .Files.Keys.Should()
            .BeEquivalentTo($"{NAME}.json", $"{NAME}.png", $"{NAME}_spritesheet.json");
        bundle.Files[$"{NAME}.png"].Should().Equal(IMAGE);

        // Its lists are arrays that carry their key; the spritesheet is beside it, not in it.
        var data = bundle.AssetData(NAME)!;

        data["type"]!.GetValue<string>().Should().Be(NAME);
        data.ContainsKey("spritesheet").Should().BeFalse();
        data["assets"]!
            .AsArray()
            .Select(x => x!["name"]!.GetValue<string>())
            .Should()
            .Equal($"{NAME}_64_a_2_0", $"{NAME}_64_a_4_0", $"{NAME}_icon_a");
        var visualization = data["visualizations"]![0]!;

        visualization["directions"]!
            .AsArray()
            .Select(x => x!["id"]!.GetValue<double>())
            .Should()
            .Equal(2, 4);
        visualization["animations"]![0]!["layers"]![0]!["frameSequences"]![0]!["frames"]![0]!["id"]!
            .GetValue<int>()
            .Should()
            .Be(0);

        // A frame carrying the library's name in front of the asset's is named as the asset; one
        // named as an asset already keeps its name.
        var sheet = JsonNode.Parse(bundle.Files[$"{NAME}_spritesheet.json"])!;

        sheet["frames"]!
            .AsObject()
            .Select(x => x.Key)
            .Should()
            .Equal($"{NAME}_64_a_2_0", $"{NAME}_icon_a");
        sheet["meta"]!["image"]!.GetValue<string>().Should().Be($"{NAME}.png");
    }

    [Fact]
    public void The_furniture_reader_reads_an_old_bundle_as_a_current_one()
    {
        var info = FurnitureAssetReader.Read(Legacy(), NAME);

        info.Logic.Should().Be("furniture_dice");
        info.States.Should().Be(2);
        info.OtherAnimations.Should().Equal(-1, 101);
        info.DimensionX.Should().Be(2);
        info.DimensionY.Should().Be(1);
        info.DimensionZ.Should().Be(0.5);
        info.Directions.Should().Equal(90, 180);
        info.Colors.Should().Equal(0, 1, 2);
    }

    [Fact]
    public void An_old_bundle_of_gzipd_files_reads_as_one_of_zlibd_files()
    {
        // Bundles in the wild hold gzip members (1f 8b) as well as zlib streams.
        var gzipped = Legacy(gzip: true);

        gzipped[2 + 2 + $"{NAME}.json".Length + 4].Should().Be(0x1F);
        LegacyNitroBundle.IsLegacy(gzipped).Should().BeTrue();

        var files = LegacyNitroBundle.Read(gzipped);
        var zlibbed = LegacyNitroBundle.Read(Legacy());

        files.Keys.Should().Equal(zlibbed.Keys);
        files[$"{NAME}.json"].Should().Equal(zlibbed[$"{NAME}.json"]);
        files[$"{NAME}.png"].Should().Equal(IMAGE);

        FurnitureAssetReader.Read(gzipped, NAME).Logic.Should().Be("furniture_dice");
        NitroBundle
            .Read(NitroConverter.Convert(gzipped, NAME).Write())
            .Files[$"{NAME}.png"]
            .Should()
            .Equal(IMAGE);
    }

    [Fact]
    public void A_file_of_an_old_bundle_that_is_not_zlib_data_is_refused()
    {
        var legacy = Legacy();

        // The header still says zlib; its first block is of the reserved type, which none is.
        legacy[2 + 2 + $"{NAME}.json".Length + 4 + 2] = 0xFF;

        var read = () => LegacyNitroBundle.Read(legacy);

        read.Should().Throw<AssetFormatException>();
    }

    /// <summary>An old bundle of a chest: a dice's logic, 2 by 1 and half a tile high, two states, three colours.</summary>
    internal static byte[] Legacy(string name = NAME, bool gzip = false)
    {
        JsonObject Frames(int id) =>
            new()
            {
                ["0"] = new JsonObject
                {
                    ["frameSequences"] = new JsonObject
                    {
                        ["0"] = new JsonObject
                        {
                            ["frames"] = new JsonObject { ["0"] = new JsonObject { ["id"] = id } },
                        },
                    },
                },
            };

        JsonObject Color(int rgb) =>
            new() { ["layers"] = new JsonObject { ["0"] = new JsonObject { ["color"] = rgb } } };

        var data = new JsonObject
        {
            ["name"] = name,
            ["logicType"] = "furniture_dice",
            ["visualizationType"] = "furniture_animated",
            ["assets"] = new JsonObject
            {
                [$"{name}_64_a_2_0"] = new JsonObject { ["x"] = 1, ["y"] = 2 },
                [$"{name}_64_a_4_0"] = new JsonObject
                {
                    ["source"] = $"{name}_64_a_2_0",
                    ["flipH"] = true,
                },
                [$"{name}_icon_a"] = new JsonObject(),
            },
            ["logic"] = new JsonObject
            {
                ["model"] = new JsonObject
                {
                    ["dimensions"] = new JsonObject
                    {
                        ["x"] = 2,
                        ["y"] = 1,
                        ["z"] = 0.5,
                    },
                    ["directions"] = new JsonArray(90, 180),
                },
            },
            ["visualizations"] = new JsonArray(
                new JsonObject
                {
                    ["angle"] = 45,
                    ["layerCount"] = 1,
                    ["size"] = 64,
                    ["layers"] = new JsonObject { ["0"] = new JsonObject() },
                    ["directions"] = new JsonObject
                    {
                        ["2"] = new JsonObject(),
                        ["4"] = new JsonObject(),
                    },
                    ["colors"] = new JsonObject
                    {
                        ["0"] = Color(16777215),
                        ["1"] = Color(14762520),
                        ["2"] = Color(15108924),
                    },
                    ["animations"] = new JsonObject
                    {
                        ["0"] = new JsonObject { ["layers"] = Frames(0) },
                        ["1"] = new JsonObject { ["layers"] = Frames(1) },
                        ["-1"] = new JsonObject { ["layers"] = Frames(2) },
                        ["101"] = new JsonObject { ["transitionTo"] = 1, ["layers"] = Frames(3) },
                    },
                }
            ),
            ["spritesheet"] = new JsonObject
            {
                ["frames"] = new JsonObject
                {
                    [$"{name}_{name}_64_a_2_0"] = new JsonObject
                    {
                        ["frame"] = new JsonObject
                        {
                            ["x"] = 0,
                            ["y"] = 0,
                            ["w"] = 1,
                            ["h"] = 1,
                        },
                    },
                    [$"{name}_icon_a"] = new JsonObject
                    {
                        ["frame"] = new JsonObject
                        {
                            ["x"] = 1,
                            ["y"] = 0,
                            ["w"] = 1,
                            ["h"] = 1,
                        },
                    },
                },
                ["meta"] = new JsonObject { ["image"] = $"{name}.png" },
            },
        };

        return Pack(
            new Dictionary<string, byte[]>
            {
                [$"{name}.json"] = Encoding.UTF8.GetBytes(data.ToJsonString()),
                [$"{name}.png"] = IMAGE,
            },
            gzip ? Gzip : Zlib
        );
    }

    private static byte[] Pack(
        IReadOnlyDictionary<string, byte[]> files,
        Func<byte[], byte[]> compress
    )
    {
        using var output = new MemoryStream();
        Span<byte> number = stackalloc byte[4];

        BinaryPrimitives.WriteUInt16BigEndian(number, (ushort)files.Count);
        output.Write(number[..2]);

        foreach (var (name, raw) in files)
        {
            var nameBytes = Encoding.UTF8.GetBytes(name);
            var data = compress(raw);

            BinaryPrimitives.WriteUInt16BigEndian(number, (ushort)nameBytes.Length);
            output.Write(number[..2]);
            output.Write(nameBytes);
            BinaryPrimitives.WriteUInt32BigEndian(number, (uint)data.Length);
            output.Write(number);
            output.Write(data);
        }

        return output.ToArray();
    }

    private static byte[] Zlib(byte[] raw)
    {
        using var output = new MemoryStream();

        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(raw);

        return output.ToArray();
    }

    private static byte[] Gzip(byte[] raw)
    {
        using var output = new MemoryStream();

        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
            gzip.Write(raw);

        return output.ToArray();
    }
}
