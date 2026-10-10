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
/// A photo's render data arrives zlib-compressed, and a few KB can inflate to gigabytes. The
/// photo lab's RenderRoom is refused (an empty storage url, nothing written) once the data inflates
/// past <see cref="CameraConfig.MaxRenderDataBytes"/>, and it counts toward the day's renders
/// whether or not it was a render, so sending it again and again is throttled too.
/// </summary>
public class CameraRenderDataLimitTests
{
    private const int PLAYER = 101;

    private readonly Fakes _fakes = new();
    private readonly string _storage = Path.Combine(
        Path.GetTempPath(),
        "turbo-tests",
        Path.GetRandomFileName()
    );
    private readonly RenderRoomMessageHandler _handler;

    public CameraRenderDataLimitTests()
    {
        var config = Options.Create(
            new CameraConfig
            {
                StoragePath = _storage,
                RendererCommand = "",
                RenderLimitPerDay = 2,
                MaxRenderDataBytes = 4096,
            }
        );

        _handler = new RenderRoomMessageHandler(
            new CameraPhotoStore(config),
            new CameraRenderer(config, NullLogger<CameraRenderer>.Instance),
            config
        );
    }

    private static byte[] Deflated(string text)
    {
        using var output = new MemoryStream();

        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(Encoding.UTF8.GetBytes(text));

        return output.ToArray();
    }

    private async Task<string> SendAsync(byte[] data)
    {
        var session = _fakes.Create<ISessionContext>("session");

        await _handler.HandleAsync(
            new RenderRoomMessage { Data = data },
            new MessageContext(session, PLAYER, 0),
            CancellationToken.None
        );

        return _fakes
            .Log.Of("SendComposerAsync")
            .Select(x => x.Args[0])
            .OfType<CameraStorageUrlMessageComposer>()
            .Last()
            .Url;
    }

    private string[] StoredPhotos =>
        Directory.Exists(Path.Combine(_storage, "photos"))
            ? Directory.GetFiles(Path.Combine(_storage, "photos"))
            : [];

    [Fact]
    public async Task DataThatInflatesPastTheLimitIsRefused()
    {
        // 64 MB of one character compresses to a few tens of KB.
        var bomb = Deflated(new string('a', 64 * 1024 * 1024));

        var url = await SendAsync(bomb);

        url.Should().BeEmpty();
        StoredPhotos.Should().BeEmpty();
    }

    [Fact]
    public async Task ARenderWithinTheLimitIsStored()
    {
        var url = await SendAsync(Deflated("{\"planes\":[]}"));

        url.Should().StartWith("photos/");
        StoredPhotos.Should().ContainSingle();
    }

    [Fact]
    public async Task DataThatIsNotARenderCountsTowardTheDaysLimit()
    {
        await SendAsync([1, 2, 3]);
        await SendAsync(Deflated(new string('a', 8192)));

        var url = await SendAsync(Deflated("{\"planes\":[]}"));

        url.Should().BeEmpty("the day's two renders were used by data that was not one");
        StoredPhotos.Should().BeEmpty();
    }
}
