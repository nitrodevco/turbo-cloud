using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Turbo.Assets.Swf;

namespace Turbo.Tests.Support;

/// <summary>
/// Habbo's AIR client as it ships: a <c>HabboWin.zip</c> holding <c>HabboAir.swf</c>, whose
/// library classes embed their bitmaps as the compiler lays an <c>[Embed]</c> out - the bitmap a
/// class of its own in the symbol table (<c>icon_png$547e…</c>), and the library class's static
/// initializer setting its static field to it (<c>getlex</c>, <c>findproperty</c>, <c>swap</c>,
/// <c>setproperty</c>).
/// </summary>
public static class TestAirClient
{
    /// <summary>An embed: the field, the class its bitmap gets, and the tag (code and body after the character id).</summary>
    public sealed record Embed(string Field, string AssetClass, int Code, byte[] Body);

    /// <summary>A bitmap embedded as Habbo's are: a DefineBitsJPEG2 holding a PNG.</summary>
    public static Embed Png(string field, string assetClass, byte[] png) =>
        new(field, assetClass, SwfLibrary.TAG_DEFINE_BITS_JPEG2, png);

    /// <summary>A bitmap embedded as a DefineBitsLossless2 of premultiplied ARGB.</summary>
    public static Embed Argb(string field, string assetClass, int width, int height, byte[] argb)
    {
        var body = new MemoryStream();
        var packed = new MemoryStream();

        using (var zlib = new ZLibStream(packed, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(argb);

        body.WriteByte(5);
        Write16(body, width);
        Write16(body, height);
        body.Write(packed.ToArray());

        return new(field, assetClass, SwfLibrary.TAG_DEFINE_BITS_LOSSLESS2, body.ToArray());
    }

    /// <summary>An embedded file that is no bitmap (an XML): a DefineBinaryData.</summary>
    public static Embed Binary(string field, string assetClass, string text) =>
        new(
            field,
            assetClass,
            SwfLibrary.TAG_DEFINE_BINARY_DATA,
            [0, 0, 0, 0, .. Encoding.UTF8.GetBytes(text)]
        );

    /// <summary>
    /// A <c>HabboWin.zip</c> with the AIR runtime's files and <c>HabboAir.swf</c>, dated when Habbo
    /// built it (to the even second a zip keeps).
    /// </summary>
    public static byte[] Zip(byte[] swf, DateTimeOffset? builtAt = null)
    {
        var output = new MemoryStream();

        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var exe = zip.CreateEntry("Habbo.exe").Open())
                exe.Write("MZ"u8);

            var air = zip.CreateEntry("HabboAir.swf");

            // A zip keeps the wall clock alone, which is read back as this machine's.
            if (builtAt is { } built)
                air.LastWriteTime = built.ToLocalTime();

            using var entry = air.Open();

            entry.Write(swf);
        }

        return output.ToArray();
    }

    /// <summary>A compressed SWF whose classes embed these, by class name.</summary>
    public static byte[] Swf(params (string ClassName, Embed[] Embeds)[] classes)
    {
        var tags = new MemoryStream();
        var symbols = new List<(int Id, string Name)>();
        var id = 1;

        foreach (var embed in classes.SelectMany(x => x.Embeds))
        {
            var body = new MemoryStream();

            Write16(body, id);
            body.Write(embed.Body);
            Tag(tags, embed.Code, body.ToArray());
            symbols.Add((id++, embed.AssetClass));
        }

        var doAbc = new MemoryStream();

        Write32(doAbc, 1);
        doAbc.Write("frame1\0"u8);
        doAbc.Write(Abc(classes));
        Tag(tags, SwfLibrary.TAG_DO_ABC, doAbc.ToArray());

        var symbolTag = new MemoryStream();

        Write16(symbolTag, symbols.Count + 1);
        Write16(symbolTag, 0);
        symbolTag.Write("HabboAir\0"u8);

        foreach (var (symbolId, name) in symbols)
        {
            Write16(symbolTag, symbolId);
            symbolTag.Write(Encoding.UTF8.GetBytes(name + "\0"));
        }

        Tag(tags, SwfLibrary.TAG_SYMBOL_CLASS, symbolTag.ToArray());
        Tag(tags, 0, []);

        var movie = new MemoryStream();

        movie.Write([0x00, 0x00, 0x18, 0x01, 0x00]);
        movie.Write(tags.ToArray());

        var packed = new MemoryStream();

        using (var zlib = new ZLibStream(packed, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(movie.ToArray());

        var file = new MemoryStream();

        file.Write("CWS"u8);
        file.WriteByte(32);
        Write32(file, (int)movie.Length + 8);
        file.Write(packed.ToArray());

        return file.ToArray();
    }

    /// <summary>
    /// An ABC block declaring each class with a static field per embed, set in its static initializer
    /// (after an instruction that sets nothing, as a real one has around them).
    /// </summary>
    private static byte[] Abc((string ClassName, Embed[] Embeds)[] classes)
    {
        var strings = new List<string>();
        var names = new Dictionary<string, int>(StringComparer.Ordinal);

        int Name(string name)
        {
            if (!names.TryGetValue(name, out var index))
            {
                strings.Add(name);
                names[name] = index = strings.Count;
            }

            return index;
        }

        foreach (var (className, embeds) in classes)
        {
            Name(className);

            foreach (var embed in embeds)
            {
                Name(embed.Field);
                Name(embed.AssetClass);
            }
        }

        var abc = new MemoryStream();

        Write16(abc, 16);
        Write16(abc, 46);
        U30(abc, 0); // ints
        U30(abc, 0); // uints
        U30(abc, 0); // doubles
        U30(abc, strings.Count + 1);

        foreach (var text in strings)
        {
            var bytes = Encoding.UTF8.GetBytes(text);

            U30(abc, bytes.Length);
            abc.Write(bytes);
        }

        // One namespace (the package's, unnamed: string 0), no sets.
        U30(abc, 2);
        abc.WriteByte(0x16);
        U30(abc, 0);
        U30(abc, 0);

        // A QName for each string, at the string's own index.
        U30(abc, strings.Count + 1);

        for (var i = 1; i <= strings.Count; i++)
        {
            abc.WriteByte(0x07);
            U30(abc, 1);
            U30(abc, i);
        }

        // Each class's instance and static initializers.
        U30(abc, classes.Length * 2);

        for (var i = 0; i < classes.Length * 2; i++)
        {
            U30(abc, 0);
            U30(abc, 0);
            U30(abc, 0);
            abc.WriteByte(0);
        }

        U30(abc, 0); // metadata
        U30(abc, classes.Length);

        for (var i = 0; i < classes.Length; i++)
        {
            U30(abc, names[classes[i].ClassName]);
            U30(abc, 0);
            abc.WriteByte(0x01);
            U30(abc, 0);
            U30(abc, i * 2);
            U30(abc, 0);
        }

        for (var i = 0; i < classes.Length; i++)
        {
            U30(abc, (i * 2) + 1);
            U30(abc, classes[i].Embeds.Length);

            foreach (var embed in classes[i].Embeds)
            {
                U30(abc, names[embed.Field]);
                abc.WriteByte(0);
                U30(abc, 0);
                U30(abc, 0);
                U30(abc, 0);
            }
        }

        U30(abc, 0); // scripts
        U30(abc, classes.Length);

        for (var i = 0; i < classes.Length; i++)
        {
            var code = new MemoryStream();

            code.Write([0xd0, 0x30]); // getlocal0, pushscope

            foreach (var embed in classes[i].Embeds)
            {
                code.Write([0x24, 0x05, 0x29]); // pushbyte 5, pop
                code.WriteByte(0x60);
                U30(code, names[embed.AssetClass]);
                code.WriteByte(0x5e);
                U30(code, names[embed.Field]);
                code.WriteByte(0x2b);
                code.WriteByte(0x61);
                U30(code, names[embed.Field]);
            }

            code.WriteByte(0x47); // returnvoid

            U30(abc, (i * 2) + 1);
            U30(abc, 2);
            U30(abc, 1);
            U30(abc, 0);
            U30(abc, 1);
            U30(abc, (int)code.Length);
            abc.Write(code.ToArray());
            U30(abc, 0);
            U30(abc, 0);
        }

        return abc.ToArray();
    }

    private static void Tag(Stream output, int code, byte[] data)
    {
        Write16(output, (code << 6) | 0x3f);
        Write32(output, data.Length);
        output.Write(data);
    }

    private static void U30(Stream output, int value)
    {
        do
        {
            var b = value & 0x7f;

            value >>= 7;
            output.WriteByte((byte)(value > 0 ? b | 0x80 : b));
        } while (value > 0);
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

        BinaryPrimitives.WriteUInt32LittleEndian(bytes, (uint)value);
        output.Write(bytes);
    }
}
