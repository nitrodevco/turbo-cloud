using System.IO.Compression;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.PacketHandlers.Camera;
using Turbo.Primitives.Messages.Outgoing.Camera;
using Turbo.Primitives.Messages.Outgoing.Turbo;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.PacketHandlers;

/// <summary>
/// A session's packets are handled one at a time, and a camera render waits for one of two
/// hotel-wide slots and then runs for up to 15 s. Awaited in the handler, it held up everything
/// else the player sent meanwhile (walking, chat). The capture now runs off the packet: the
/// handler returns while the render is drawn, and the answer reaches the player through their
/// presence when it is done. These drive client bytes through the revision's parser to the
/// handler, with a renderer that draws only when the test lets it.
/// </summary>
public sealed class CameraCaptureOffThePacketTests : IDisposable
{
    private const int PLAYER = 101;
    private const int ROOM = 7;

    private static readonly TimeSpan Prompt = TimeSpan.FromSeconds(5);

    private readonly PacketHarness _harness = new();
    private readonly TaskCompletionSource<bool> _render = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );
    private readonly string _storage = Path.Combine(
        Path.GetTempPath(),
        "turbo-tests",
        Path.GetRandomFileName()
    );

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public CameraCaptureOffThePacketTests()
    {
        var config = Options.Create(
            new CameraConfig
            {
                StoragePath = _storage,
                RenderLimitPerDay = 5,
                ThumbnailLimitPerDay = 5,
            }
        );
        var fakes = _harness.Fakes;

        fakes.Handlers["RenderAsync"] = _ => _render.Task;
        fakes.Handlers["GetControllerLevelAsync"] = _ => Task.FromResult(RoomControllerType.Owner);

        // The server's singletons: one store and one set of running captures for every packet.
        _harness.Resolver.Overrides[typeof(IOptions<CameraConfig>)] = config;
        _harness.Resolver.Overrides[typeof(ICameraRenderer)] = fakes.Create<ICameraRenderer>();
        _harness.Resolver.Overrides[typeof(CameraPhotoStore)] = new CameraPhotoStore(config);
        _harness.Resolver.Overrides[typeof(CameraCaptureJobs)] = new CameraCaptureJobs(
            fakes.Create<Orleans.IGrainFactory>(),
            fakes.Create<IHostApplicationLifetime>(),
            NullLogger<CameraCaptureJobs>.Instance
        );
    }

    public void Dispose()
    {
        _render.TrySetResult(false);

        if (Directory.Exists(_storage))
            Directory.Delete(_storage, recursive: true);
    }

    private static byte[] Deflated(string json)
    {
        using var output = new MemoryStream();

        using (var zlib = new ZLibStream(output, CompressionLevel.Fastest, leaveOpen: true))
            zlib.Write(Encoding.UTF8.GetBytes(json));

        return output.ToArray();
    }

    private static byte[] RenderData(int? roomId = null) =>
        PacketHarness.Payload(w =>
        {
            var data = Deflated("{\"planes\":[]}");

            if (roomId is { } id)
                w.Int(id);

            w.Int(data.Length);

            foreach (var b in data)
                w.Byte(b);
        });

    /// <summary>Sends the packet and expects its handler to return while the render is still drawing.</summary>
    private async Task SendAsync(string incoming, byte[] payload)
    {
        await _harness
            .SendAsync(PacketHarness.Incoming(incoming), payload, PLAYER, ROOM)
            .WaitAsync(Prompt, Ct);
    }

    private List<T> Sent<T>(Type through)
        where T : IComposer =>
        _harness
            .Fakes.Log.Of("SendComposerAsync")
            .Where(x => x.Interface == through)
            .Where(x => through != typeof(IPlayerPresenceGrain) || Equals(x.Key, (long)PLAYER))
            .Select(x => x.Args[0])
            .OfType<T>()
            .ToList();

    private List<T> ToPlayer<T>()
        where T : IComposer => Sent<T>(typeof(IPlayerPresenceGrain));

    private List<T> ToSession<T>()
        where T : IComposer => Sent<T>(typeof(ISessionContext));

    [Fact]
    public async Task APhoto_IsAnsweredWhenDrawn_WithoutHoldingUpTheSession()
    {
        await SendAsync("RenderRoomMessageEvent", RenderData());

        ToPlayer<CameraStorageUrlMessageComposer>().Should().BeEmpty("it is still being drawn");

        _render.SetResult(true);
        await CountWait.ForAsync(() => ToPlayer<CameraStorageUrlMessageComposer>().Count, 1);

        ToPlayer<CameraStorageUrlMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.Url.Should()
            .StartWith("photos/");
        ToSession<CameraStorageUrlMessageComposer>().Should().BeEmpty();
    }

    [Fact]
    public async Task APhotoSentWhileTheLastIsDrawn_IsRefusedAtOnce_AndNotCounted()
    {
        await SendAsync("RenderRoomMessageEvent", RenderData());
        await SendAsync("RenderRoomMessageEvent", RenderData());

        ToSession<CameraStorageUrlMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.Url.Should()
            .BeEmpty("one capture per player is drawn at a time");

        _render.SetResult(true);
        await CountWait.ForAsync(() => ToPlayer<CameraStorageUrlMessageComposer>().Count, 1);

        ToPlayer<CameraStorageUrlMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.Url.Should()
            .StartWith("photos/");
        Directory
            .GetFiles(Path.Combine(_storage, "photos"), "*.json")
            .Should()
            .ContainSingle("the refused photo was not stored");
    }

    [Fact]
    public async Task ARoomThumbnail_IsAnsweredWhenDrawn_WithoutHoldingUpTheSession()
    {
        await SendAsync("RenderRoomThumbnailMessageEvent", RenderData());

        ToPlayer<ThumbnailStatusMessageComposer>().Should().BeEmpty("it is still being drawn");

        _render.SetResult(true);
        await CountWait.ForAsync(() => ToPlayer<ThumbnailStatusMessageComposer>().Count, 1);

        var status = ToPlayer<ThumbnailStatusMessageComposer>().Should().ContainSingle().Subject;

        status.IsOk.Should().BeTrue();
        status.IsRenderLimitHit.Should().BeFalse();
    }

    [Fact]
    public async Task AStarterRoomPictureThatFailsToDraw_IsReportedWhenTheRenderEnds()
    {
        var directory = _harness.Fakes.Create<IStarterRoomDirectory>();

        Directory.CreateDirectory(_storage);
        _harness.Resolver.Overrides[typeof(IStarterRoomDirectory)] = directory;
        _harness.Fakes.Handlers["GetAsync"] = _ =>
            Task.FromResult<StarterRoomSnapshot?>(
                new StarterRoomSnapshot(new RoomId(ROOM), "Starter", "", 0, null, null, null)
            );
        _harness.Fakes.Handlers["GetPicturePaths"] = _ => new StarterRoomPicturePaths(
            Path.Combine(_storage, "starter.json"),
            Path.Combine(_storage, "starter-scene.png"),
            Path.Combine(_storage, "starter.png")
        );

        await SendAsync("TurboStarterRoomPictureMessageEvent", RenderData(ROOM));

        ToPlayer<TurboStarterRoomPictureResultMessage>().Should().BeEmpty();

        _render.SetResult(false);
        await CountWait.ForAsync(() => ToPlayer<TurboStarterRoomPictureResultMessage>().Count, 1);

        var result = ToPlayer<TurboStarterRoomPictureResultMessage>()
            .Should()
            .ContainSingle()
            .Subject;

        result.RoomId.Should().Be(ROOM);
        result.Ok.Should().BeFalse();
        _harness.Fakes.Log.Of("PictureTakenAsync").Should().BeEmpty();
    }
}
