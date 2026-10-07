using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Turbo.Gamedata;

/// <summary>Gamedata content as it is kept and named: gzipped, and by its SHA-1, as Habbo names its files.</summary>
internal static class GamedataBytes
{
    public static string Hash(byte[] content) => Convert.ToHexStringLower(SHA1.HashData(content));

    public static byte[] Compress(byte[] content)
    {
        using var output = new MemoryStream();

        using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
            gzip.Write(content);

        return output.ToArray();
    }

    public static byte[] Decompress(byte[] gzipped)
    {
        using var input = new MemoryStream(gzipped);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();

        gzip.CopyTo(output);

        return output.ToArray();
    }
}
