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
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.PacketHandlers;

/// <summary>
/// A room thumbnail is only taken by a player who may edit the room's settings: the client shows
/// the button to them alone (<c>RoomInfoViewCtrl</c>, <c>canEditRoomSettings</c>), so a visitor's
/// RenderRoomThumbnail is refused (not ok, no file) and the owner's is stored.
/// </summary>
public class RenderRoomThumbnailRightsTests
{
    private const int PLAYER = 101;
    private const int ROOM = 7;

    private readonly Fakes _fakes = new();
    private readonly string _storage = Path.Combine(
        Path.GetTempPath(),
        "turbo-tests",
        Path.GetRandomFileName()
    );
    private RoomControllerType _level = RoomControllerType.None;

    private RenderRoomThumbnailMessageHandler Handler()
    {
        _fakes.Handlers["GetControllerLevelAsync"] = _ => Task.FromResult(_level);

        var config = Options.Create(
            new CameraConfig
            {
                StoragePath = _storage,
                RendererCommand = "",
                ThumbnailLimitPerDay = 5,
            }
        );

        return new RenderRoomThumbnailMessageHandler(
            new CameraPhotoStore(config),
            new CameraRenderer(config, NullLogger<CameraRenderer>.Instance),
            _fakes.Create<Orleans.IGrainFactory>(),
            config
        );
    }

    private static byte[] Deflated(string json)
    {
        using var output = new MemoryStream();

        using (var zlib = new ZLibStream(output, CompressionLevel.Fastest, leaveOpen: true))
            zlib.Write(Encoding.UTF8.GetBytes(json));

        return output.ToArray();
    }

    private async Task<ThumbnailStatusMessageComposer> SendAsync()
    {
        var session = _fakes.Create<ISessionContext>("session");

        await Handler()
            .HandleAsync(
                new RenderRoomThumbnailMessage { Data = Deflated("{\"planes\":[]}") },
                new MessageContext(session, PLAYER, ROOM),
                CancellationToken.None
            );

        return _fakes
            .Log.Of("SendComposerAsync")
            .Select(x => x.Args[0])
            .OfType<ThumbnailStatusMessageComposer>()
            .Single();
    }

    private string ThumbnailJson => Path.Combine(_storage, "thumbnails", $"{ROOM}.json");

    [Fact]
    public async Task AVisitorsThumbnailIsRefused()
    {
        _level = RoomControllerType.Rights;

        var status = await SendAsync();

        status.IsOk.Should().BeFalse();
        status.IsRenderLimitHit.Should().BeFalse();
        File.Exists(ThumbnailJson).Should().BeFalse();
    }

    [Fact]
    public async Task TheOwnersThumbnailIsStored()
    {
        _level = RoomControllerType.Owner;

        var status = await SendAsync();

        status.IsOk.Should().BeTrue();
        status.IsRenderLimitHit.Should().BeFalse();
        File.ReadAllText(ThumbnailJson).Should().Be("{\"planes\":[]}");
    }
}
