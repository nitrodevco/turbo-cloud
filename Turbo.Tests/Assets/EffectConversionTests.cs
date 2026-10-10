using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;
using FluentAssertions;
using Turbo.Assets.Conversion;
using Turbo.Assets.Swf;
using Xunit;

namespace Turbo.Tests.Assets;

/// <summary>
/// Avatar effect libraries (<c>fx</c>) converted as nitro-studio's converter converts them: the
/// <c>animation</c> document mapped to the <c>animations</c> the client's effects play, and the
/// effect's assets kept at every size but its <c>sh_</c> shadows. All 252 of Habbo's effect
/// libraries were compared with the studio's output when this was ported, byte for byte.
/// </summary>
public sealed class EffectConversionTests
{
    private const string LIB = "TestFx";

    [Fact]
    public void a_real_effect_converts_to_the_asset_data_the_studio_wrote()
    {
        var bundle = NitroConverter.Convert(Fixture("Duck.swf"), null, AssetDataFilter.EFFECT);

        bundle.Files.Keys.Should().Equal("Duck.json", "Duck.png", "Duck_spritesheet.json");
        // nitro-studio's converter's own output for this library, byte for byte.
        bundle.Files["Duck.json"].Should().Equal(Fixture("Duck.nitro-studio.json"));
    }

    [Fact]
    public void an_effect_animation_is_written_as_the_studio_wrote_it()
    {
        var data = AssetData(Swf(ANIMATION));

        data.Select(x => x.Key).Should().Equal("type", "assets", "animations");
        // What the studio's AnimationMapper writes for the same XML, run through it.
        data["animations"]!
            .ToJsonString()
            .Should()
            .Be(
                """[{"name":"fx.9","desc":"Test","resetOnToggle":true,"directions":[{"offset":2}],"shadows":[{"id":"std_sd"}],"adds":[{"id":"fx9_1","align":"top","blend":"add","ink":33,"base":"1"}],"removes":[{"id":"ha"}],"sprites":[{"id":"avatar"},{"id":"fx9_2","directions":1,"member":"std_fx9_2","ink":8,"staticY":1,"directionList":[{"id":0,"dz":-1},{"id":1,"dx":2,"dy":null}]}],"frames":[{"repeats":2,"fxs":[{"id":"fx9_1","frame":3,"base":"b","action":"Move","dx":0,"dy":1,"dz":2,"dd":4,"items":[{"id":"ri","base":"1"}]}],"bodyparts":[{"id":"head","action":"Default"}]},{}],"avatars":[{"background":"fx9_2","foreground":"fx9_1","ink":33}],"overrides":[{"name":"mv_x","override":"mv","frames":[{"fxs":[{"id":"fx9_1"}]}]},{"name":"empty","override":"std"}]}]"""
            );
    }

    [Fact]
    public void an_empty_animation_is_one_animation_with_nothing_in_it()
    {
        AssetData(Swf("<animation/>"))["animations"]!.ToJsonString().Should().Be("[{}]");
        // Only an <animation> root is an effect's animation.
        AssetData(Swf("""<other name="x"/>""")).ContainsKey("animations").Should().BeFalse();
    }

    [Fact]
    public void an_effect_keeps_every_size_but_drops_its_shadows()
    {
        var effect = AssetData(Swf(ANIMATION), AssetDataFilter.EFFECT);
        var furniture = AssetData(Swf(ANIMATION));

        Names(effect).Should().Equal($"{LIB}_32_a", "h_std_fx9_1_0_0");
        // Not an avatar library: the 32 size goes and the shadow stays.
        Names(furniture).Should().Equal("sh_std_fx9_1_0_0", "h_std_fx9_1_0_0");
    }

    // ------------------------------------------------------------------ files

    private const string MANIFEST = """
        <manifest><library name="TestFx" version="0.1"><assets>
          <asset name="TestFx_32_a" mimeType="image/png"><param key="offset" value="1,2"/></asset>
          <asset name="sh_std_fx9_1_0_0" mimeType="image/png"><param key="offset" value="3,4"/></asset>
          <asset name="h_std_fx9_1_0_0" mimeType="image/png"><param key="offset" value="-5,6"/></asset>
        </assets></library></manifest>
        """;

    private const string ANIMATION = """
        <animation name="fx.9" desc="Test" resetOnToggle="true">
          <direction offset="2"/>
          <shadow id="std_sd"/>
          <add id="fx9_1" align="top" blend="add" ink="33" base="1"/>
          <remove id="ha"/>
          <sprite id="avatar"/>
          <sprite id="fx9_2" member="std_fx9_2" directions="1" staticY="1" ink="8">
            <direction id="0" dz="-1"/>
            <direction id="1" dx="2" dy="x"/>
          </sprite>
          <frame repeats="2">
            <fx id="fx9_1" frame="3" base="b" action="Move" dx="-0" dy="1" dz="2" dd="4">
              <item id="ri" base="1"/>
            </fx>
            <bodypart id="head" action="Default"/>
          </frame>
          <frame/>
          <avatar ink="33" foreground="fx9_1" background="fx9_2"/>
          <override name="mv_x" override="mv">
            <frame><fx id="fx9_1"/></frame>
          </override>
          <override name="empty" override="std"/>
        </animation>
        """;

    private static JsonObject AssetData(byte[] swf, string? assetType = null) =>
        JsonNode
            .Parse(NitroConverter.Convert(swf, null, assetType).Files[$"{LIB}.json"])!
            .AsObject();

    private static IEnumerable<string> Names(JsonObject data) =>
        data["assets"]!.AsArray().Select(x => x!["name"]!.GetValue<string>());

    private static byte[] Fixture(string name)
    {
        using var stream = typeof(EffectConversionTests).Assembly.GetManifestResourceStream(
            $"Assets.Fixtures.{name}"
        )!;
        using var copy = new MemoryStream();

        stream.CopyTo(copy);

        return copy.ToArray();
    }

    /// <summary>An effect SWF as Habbo builds one, without images: its manifest and animation.</summary>
    private static byte[] Swf(string animation)
    {
        var tags = new MemoryStream();
        var symbols = new List<(int Id, string Name)> { (0, LIB) };
        var id = 1;

        foreach (var (name, xml) in new[] { ("manifest", MANIFEST), ("animation", animation) })
        {
            var data = new MemoryStream();

            Write16(data, id);
            data.Write(new byte[4]);
            data.Write(Encoding.UTF8.GetBytes(xml));
            Tag(tags, SwfLibrary.TAG_DEFINE_BINARY_DATA, data.ToArray());
            symbols.Add((id++, $"{LIB}_{name}"));
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

        var file = new MemoryStream();
        var body = tags.ToArray();

        file.Write("FWS"u8);
        file.WriteByte(10);
        Write32(file, body.Length + 13);
        file.Write([0x00, 0x00, 0x18, 0x01, 0x00]);
        file.Write(body);

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
