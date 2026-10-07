using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Turbo.Assets;
using Turbo.Assets.Furniture;
using Turbo.Assets.Hab;
using Turbo.Assets.Swf;
using Xunit;

namespace Turbo.Tests.Assets;

/// <summary>
/// A furniture's asset file read for what furnidata does not say - its states above all - whether
/// it is an SWF, a <c>.hab</c> or a <c>.nitro</c>. The files are built here, as Habbo lays them out.
/// </summary>
public sealed class FurnitureAssetReaderTests
{
    private const string LAMP = "test_lamp";

    private const string INDEX =
        """<?xml version="1.0"?><object type="test_lamp" visualization="furniture_animated" logic="furniture_multistate"/>""";

    private const string LOGIC = """
        <objectData type="test_lamp">
          <model>
            <dimensions x="1" y="2" z="1.5"/>
            <directions><direction id="90"/><direction id="180"/></directions>
          </model>
        </objectData>
        """;

    // States 0 to 2 - state 0 with no animation of its own - a transition marked as one, a
    // transition only numbered as one, and a special animation (a dice rolling).
    private const string VISUALIZATION = """
        <visualizationData type="test_lamp">
          <!-- a comment the reader ignores -->
          <graphics>
            <visualization size="32" layerCount="2" angle="45"/>
            <visualization size="64" layerCount="3" angle="45">
              <colors><color id="1"/><color id="3"/></colors>
              <animations>
                <animation id="1"/>
                <animation id="2"/>
                <animation id="5" transitionTo="2"/>
                <animation id="101"/>
                <animation id="-1"/>
              </animations>
            </visualization>
          </graphics>
        </visualizationData>
        """;

    [Fact]
    public void an_swf_says_its_states_logic_size_directions_and_colours()
    {
        var info = FurnitureAssetReader.Read(Swf(compressed: true));

        Expect(info);
    }

    [Fact]
    public void an_uncompressed_swf_reads_the_same()
    {
        Expect(FurnitureAssetReader.Read(Swf(compressed: false)));
    }

    [Fact]
    public void a_hab_reads_through_its_xml_entries()
    {
        Expect(FurnitureAssetReader.Read(Hab(), LAMP));
    }

    [Fact]
    public void a_nitro_bundle_reads_through_its_asset_data_in_either_layout()
    {
        var info = FurnitureAssetReader.Read(Nitro(keyedById: false), LAMP);
        var legacy = FurnitureAssetReader.Read(Nitro(keyedById: true), LAMP);

        Expect(info);
        Expect(legacy);
    }

    [Fact]
    public void furniture_with_no_animations_has_no_states()
    {
        var info = FurnitureAssetReader.Read(
            Swf(
                compressed: true,
                visualization: """<visualizationData type="test_lamp"><graphics><visualization size="64" layerCount="1"/></graphics></visualizationData>"""
            )
        );

        info.States.Should().Be(0);
    }

    [Fact]
    public void a_size_of_infinity_or_nan_is_no_size()
    {
        var info = FurnitureAssetReader.FromLibrary(
            new FakeLibrary(
                ("index", INDEX),
                (
                    $"{LAMP}_logic",
                    """<objectData><model><dimensions x="Infinity" y="NaN" z="1"/></model></objectData>"""
                )
            )
        );

        info.DimensionX.Should().BeNull();
        info.DimensionY.Should().BeNull();
        info.DimensionZ.Should().Be(1);
        JsonSerializer.Serialize(info).Should().NotBeEmpty();
    }

    private sealed class FakeLibrary(params (string Name, string Xml)[] documents) : IAssetLibrary
    {
        public string DocumentClass => LAMP;

        public string? GetXml(string name) => documents.FirstOrDefault(x => x.Name == name).Xml;

        public byte[]? GetBinary(string name) => null;
    }

    [Fact]
    public void a_file_of_no_kind_it_knows_is_refused()
    {
        var read = () => FurnitureAssetReader.Read("not an asset"u8.ToArray());

        read.Should().Throw<AssetFormatException>();
    }

    [Fact]
    public void an_swf_claiming_to_unpack_past_the_limit_is_refused_before_unpacking()
    {
        var swf = Swf(compressed: true);

        BinaryPrimitives.WriteUInt32LittleEndian(swf.AsSpan(4), uint.MaxValue);

        var read = () => SwfLibrary.Read(swf);

        read.Should().Throw<AssetFormatException>();
    }

    private static void Expect(FurnitureAssetInfo info)
    {
        info.Type.Should().Be(LAMP);
        info.Logic.Should().Be("furniture_multistate");
        info.Visualization.Should().Be("furniture_animated");
        info.States.Should().Be(3);
        info.StateAnimations.Should().Equal(1, 2);
        info.OtherAnimations.Should().Equal(-1, 5, 101);
        info.DimensionX.Should().Be(1);
        info.DimensionY.Should().Be(2);
        info.DimensionZ.Should().Be(1.5);
        info.Directions.Should().Equal(90, 180);
        info.Colors.Should().Equal(1, 3);
        info.LayerCount.Should().Be(3);
        info.Sizes.Should().Equal(32, 64);
    }

    /// <summary>An SWF as Habbo's are: the document class as symbol 0, a binary tag per document.</summary>
    private static byte[] Swf(bool compressed, string visualization = VISUALIZATION)
    {
        var documents = new (string Name, string Xml)[]
        {
            ("index", INDEX),
            ("logic", LOGIC),
            ("visualization", VISUALIZATION == visualization ? VISUALIZATION : visualization),
        };
        var tags = new MemoryStream();
        var symbols = new MemoryStream();

        Write16(symbols, documents.Length + 1);
        Write16(symbols, 0);
        symbols.Write(Encoding.UTF8.GetBytes(LAMP + "\0"));

        for (var i = 0; i < documents.Length; i++)
        {
            var id = i + 1;
            var name =
                documents[i].Name == "index"
                    ? $"{LAMP}_index"
                    : $"{LAMP}_{LAMP}_{documents[i].Name}";
            var data = new MemoryStream();

            Write16(data, id);
            data.Write(new byte[4]);
            data.Write(Encoding.UTF8.GetBytes(documents[i].Xml));
            Tag(tags, SwfLibrary.TAG_DEFINE_BINARY_DATA, data.ToArray());

            Write16(symbols, id);
            symbols.Write(Encoding.UTF8.GetBytes(name + "\0"));
        }

        Tag(tags, SwfLibrary.TAG_SYMBOL_CLASS, symbols.ToArray());
        Tag(tags, SwfLibrary.TAG_END, []);

        // A RECT of 5-bit fields (one byte with Nbits 0 and padding), frame rate and count.
        var body = new MemoryStream();

        body.Write([0x00, 0x00, 0x18, 0x01, 0x00]);
        body.Write(tags.ToArray());

        var bodyBytes = body.ToArray();
        var file = new MemoryStream();

        file.Write(Encoding.ASCII.GetBytes(compressed ? "CWS" : "FWS"));
        file.WriteByte(10);
        Write32(file, bodyBytes.Length + 8);

        if (compressed)
        {
            using var zlib = new ZLibStream(file, CompressionLevel.Optimal, leaveOpen: true);

            zlib.Write(bodyBytes);
        }
        else
        {
            file.Write(bodyBytes);
        }

        return file.ToArray();
    }

    /// <summary>
    /// A library like the SWF, renamed so the names prove the reader uses the library's, not the
    /// SWF's: its documents are named as the client asks for them.
    /// </summary>
    private static byte[] Hab()
    {
        var payload = new MemoryStream();
        var entries = new JsonArray();

        foreach (
            var (name, xml) in new[]
            {
                ("index", INDEX),
                ($"{LAMP}_logic", LOGIC),
                ($"{LAMP}_visualization", VISUALIZATION),
            }
        )
        {
            var raw = Encoding.UTF8.GetBytes(xml);
            var stored = new MemoryStream();

            using (var zlib = new ZLibStream(stored, CompressionLevel.Optimal, leaveOpen: true))
                zlib.Write(raw);

            entries.Add(
                new JsonObject
                {
                    ["name"] = name,
                    ["mimeType"] = "text/xml",
                    ["offset"] = payload.Length,
                    ["storedLength"] = stored.Length,
                    ["originalLength"] = raw.Length,
                    ["compression"] = "deflate",
                }
            );
            payload.Write(stored.ToArray());
        }

        var manifest = Encoding.UTF8.GetBytes(
            new JsonObject
            {
                ["format"] = "hab",
                ["version"] = 1,
                ["name"] = LAMP,
                ["entries"] = entries,
            }.ToJsonString()
        );
        var manifestStored = new MemoryStream();

        using (var zlib = new ZLibStream(manifestStored, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(manifest);

        var file = new MemoryStream();

        file.Write("HAB\0"u8);
        Write16(file, 1);
        Write16(file, 1);
        Write32(file, (int)manifestStored.Length);
        Write32(file, manifest.Length);
        Write32(file, (int)payload.Length);
        file.Write(manifestStored.ToArray());
        file.Write(payload.ToArray());

        HabLibrary.IsHab(file.ToArray()).Should().BeTrue();

        return file.ToArray();
    }

    private static byte[] Nitro(bool keyedById)
    {
        JsonNode List(params JsonObject[] items)
        {
            if (!keyedById)
                return new JsonArray(items);

            var keyed = new JsonObject();

            foreach (var item in items)
                keyed[item["id"]!.ToJsonString()] = item;

            return keyed;
        }

        var data = new JsonObject
        {
            ["type"] = LAMP,
            ["logicType"] = "furniture_multistate",
            ["visualizationType"] = "furniture_animated",
            ["logic"] = new JsonObject
            {
                ["model"] = new JsonObject
                {
                    ["dimensions"] = new JsonObject
                    {
                        ["x"] = 1,
                        ["y"] = 2,
                        ["z"] = 1.5,
                    },
                    ["directions"] = new JsonArray(90, 180),
                },
            },
            ["visualizations"] = new JsonArray(
                new JsonObject { ["size"] = 32, ["layerCount"] = 2 },
                new JsonObject
                {
                    ["size"] = 64,
                    ["layerCount"] = 3,
                    ["colors"] = List(new JsonObject { ["id"] = 1 }, new JsonObject { ["id"] = 3 }),
                    ["animations"] = List(
                        new JsonObject { ["id"] = 1 },
                        new JsonObject { ["id"] = 2 },
                        new JsonObject { ["id"] = 5, ["transitionTo"] = 2 },
                        new JsonObject { ["id"] = 101 },
                        new JsonObject { ["id"] = -1 }
                    ),
                }
            ),
        };
        var file = new MemoryStream();

        using (var zip = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var entry = zip.CreateEntry($"{LAMP}.json").Open())
                JsonSerializer.Serialize(entry, data);

            using (var sheet = zip.CreateEntry($"{LAMP}_spritesheet.json").Open())
                sheet.Write("{}"u8);
        }

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
