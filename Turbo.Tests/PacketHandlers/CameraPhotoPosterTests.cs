using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Camera;
using Turbo.Primitives.Messages.Incoming.Camera;
using Turbo.Primitives.Messages.Outgoing.Camera;
using Turbo.Primitives.Networking;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.PacketHandlers;

/// <summary>
/// A bought photo is a poster, and a poster on a wall loads the photo's <c>_small</c> copy
/// (<c>FurnitureExternalImageVisualization.buildThumbnailUrl</c>: <c>photos/&lt;id&gt;_small.png</c>).
/// Only the full picture was drawn, so every poster hung blank.
/// </summary>
public sealed class CameraPhotoPosterTests
{
    private const int PLAYER = 101;

    private readonly Fakes _fakes = new();
    private readonly string _storage = Path.Combine(
        Path.GetTempPath(),
        "turbo-tests",
        Path.GetRandomFileName()
    );

    [Fact]
    public async Task A_photo_is_drawn_with_the_half_size_copy_a_poster_loads()
    {
        RequireNode();

        var config = Options.Create(
            new CameraConfig
            {
                StoragePath = _storage,
                RendererCacheDirectory = Path.Combine(_storage, "cache"),
            }
        );
        var handler = new RenderRoomMessageHandler(
            new CameraPhotoStore(config),
            new CameraRenderer(config, NullLogger<CameraRenderer>.Instance),
            config
        );
        var session = _fakes.Create<ISessionContext>("session");

        await handler.HandleAsync(
            new RenderRoomMessage
            {
                Data = Deflated(
                    """{"planes":[{"z":0,"color":255,"cornerPoints":[{"x":0,"y":0},{"x":320,"y":0},{"x":0,"y":320},{"x":320,"y":320}]}],"sprites":[]}"""
                ),
            },
            new MessageContext(session, PLAYER, 0),
            CancellationToken.None
        );

        var url = _fakes
            .Log.Of("SendComposerAsync")
            .Select(x => x.Args[0])
            .OfType<CameraStorageUrlMessageComposer>()
            .Single()
            .Url;
        var id = Path.GetFileNameWithoutExtension(url);
        var photos = Path.Combine(_storage, "photos");

        Size(Path.Combine(photos, $"{id}.png")).Should().Be((320, 320));
        Size(Path.Combine(photos, $"{id}_small.png")).Should().Be((160, 160));
    }

    private static (int Width, int Height) Size(string png)
    {
        File.Exists(png).Should().BeTrue($"{Path.GetFileName(png)} is drawn");

        var header = File.ReadAllBytes(png).AsSpan(16, 8);

        return (
            BinaryPrimitives.ReadInt32BigEndian(header[..4]),
            BinaryPrimitives.ReadInt32BigEndian(header[4..])
        );
    }

    private static byte[] Deflated(string json)
    {
        using var output = new MemoryStream();

        using (var zlib = new ZLibStream(output, CompressionLevel.Fastest, leaveOpen: true))
            zlib.Write(Encoding.UTF8.GetBytes(json));

        return output.ToArray();
    }

    private static void RequireNode()
    {
        try
        {
            using var node = Process.Start(
                new ProcessStartInfo("node", "--version")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                }
            );

            node!.WaitForExit();
        }
        catch (Exception)
        {
            Assert.Skip("node is not installed");
        }
    }
}
