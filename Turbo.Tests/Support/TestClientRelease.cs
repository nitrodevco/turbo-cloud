using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace Turbo.Tests.Support;

/// <summary>
/// A release of Habbo's HTML client as its hosts serve it: the <c>app.asar</c> (with the
/// <c>release-manifest.json</c> listing every file but itself), and the <c>native-release.json</c>
/// listing beside it. <see cref="Serve"/> puts it, and the sandbox's <c>clienturls</c> naming it,
/// on a host that serves files by address.
/// </summary>
public static class TestClientRelease
{
    public const string LISTING =
        "https://images.habbo.com/habbo-clients/classic-electron/win/prod/95_x/native-release.json";
    public const string ASAR =
        "https://images.habbo.com/habbo-clients/classic-electron/win/prod/95_x/app.asar";
    public const string VERSION = "95_classic-js-x";

    /// <summary>The release of these files, put on the host through <paramref name="serve"/>.</summary>
    public static void Serve(
        Action<string, byte[]> serve,
        params (string Path, byte[] Data)[] files
    )
    {
        var asar = Asar([("release-manifest.json", Manifest(VERSION, files)), .. files]);

        serve(
            "https://sandbox.habbo.com/gamedata/clienturls",
            JsonSerializer.SerializeToUtf8Bytes(
                new Dictionary<string, string>
                {
                    ["html-windows-version"] = VERSION,
                    ["html-windows"] = LISTING,
                }
            )
        );
        serve(LISTING, Listing(asar));
        serve(ASAR, asar);
    }

    public static byte[] Listing(byte[] asar, long? size = null, string? sha256 = null) =>
        JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                releaseId = VERSION,
                clientAsar = new
                {
                    archive = "app.asar",
                    size = size ?? asar.Length,
                    sha256 = sha256 ?? Convert.ToHexStringLower(SHA256.HashData(asar)),
                },
            }
        );

    public static byte[] Manifest(string releaseId, params (string Path, byte[] Data)[] files) =>
        JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                releaseId,
                files = files.Select(x => new
                {
                    path = x.Path,
                    size = x.Data.Length,
                    sha256 = Convert.ToHexStringLower(SHA256.HashData(x.Data)),
                }),
            }
        );

    /// <summary>An asar archive of the files: the header pickle, its JSON tree, then the data.</summary>
    public static byte[] Asar(params (string Path, byte[] Data)[] files)
    {
        var root = new Dictionary<string, object>();
        var offset = 0L;

        foreach (var (path, data) in files)
        {
            var folder = root;
            var parts = path.Split('/');

            foreach (var part in parts[..^1])
            {
                if (!folder.TryGetValue(part, out var child))
                    folder[part] = child = new Dictionary<string, object>
                    {
                        ["files"] = new Dictionary<string, object>(),
                    };

                folder = (Dictionary<string, object>)((Dictionary<string, object>)child)["files"];
            }

            folder[parts[^1]] = new
            {
                size = data.Length,
                offset = offset.ToString(CultureInfo.InvariantCulture),
            };
            offset += data.Length;
        }

        var json = JsonSerializer.SerializeToUtf8Bytes(new { files = root });
        var padded = (json.Length + 3) & ~3;
        var headerSize = 8 + padded;
        var head = new byte[16];
        var bytes = new List<byte>();

        BinaryPrimitives.WriteUInt32LittleEndian(head, 4);
        BinaryPrimitives.WriteUInt32LittleEndian(head.AsSpan(4), (uint)headerSize);
        BinaryPrimitives.WriteUInt32LittleEndian(head.AsSpan(8), (uint)(headerSize - 4));
        BinaryPrimitives.WriteUInt32LittleEndian(head.AsSpan(12), (uint)json.Length);
        bytes.AddRange(head);
        bytes.AddRange(json);
        bytes.AddRange(new byte[padded - json.Length]);

        foreach (var (_, data) in files)
            bytes.AddRange(data);

        return [.. bytes];
    }
}
