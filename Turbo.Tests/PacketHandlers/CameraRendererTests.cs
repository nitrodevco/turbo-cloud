using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.PacketHandlers.Camera;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.PacketHandlers;

/// <summary>
/// The render data a camera sends is the client's, and the renderer (<c>render.mjs</c>, run by
/// <see cref="CameraRenderer"/>) must not let it steer the server: an external image is fetched only
/// from a url the hotel allowed (never redirected elsewhere), a furniture library name cannot climb
/// out of the bundle url, and a canvas larger than a photo is refused instead of allocated.
/// It draws what the client's camera shows: the avatars' parts (Flash
/// <c>AvatarVisualization.getSpriteList</c>) from the figure library the figure map names, and the
/// photo lab's zoom and effects (<c>CameraPhotoLab.renderAllEffects</c>), each room plane's texture
/// columns and masks (Flash <c>RoomPlane.getDrawingDatas</c>) from the room content library, and a
/// flipped sprite as its asset mirrored once.
/// </summary>
public sealed class CameraRendererTests : IDisposable
{
    // A 1x1 RGBA PNG.
    private static readonly byte[] PIXEL = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg=="
    );

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-tests",
        Path.GetRandomFileName()
    );
    private readonly LocalHttpServer _server = new();
    private readonly CapturingLogger<CameraRenderer> _log = new();

    public CameraRendererTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => _server.Dispose();

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

    private async Task<(bool Drawn, string Png)> RenderAsync(
        string json,
        params string[] externalUrls
    )
    {
        RequireNode();

        var jsonPath = Path.Combine(_folder, "render.json");
        var pngPath = Path.Combine(_folder, "render.png");

        File.WriteAllText(jsonPath, json);

        var renderer = new CameraRenderer(
            Options.Create(
                new CameraConfig
                {
                    RendererCacheDirectory = Path.Combine(_folder, "cache"),
                    RendererFurniUrl = $"{_server.Url}furni/%libname%.nitro",
                    RendererRoomUrl = $"{_server.Url}room/%libname%.nitro",
                    RendererFigureUrl = $"{_server.Url}figures/%libname%.nitro",
                    RendererFigureMapUrl = $"{_server.Url}figuremap.json",
                    RendererEffectUrl = $"{_server.Url}effects/%libname%.nitro",
                    RendererEffectMapUrl = $"{_server.Url}effectmap.json",
                    RendererEffectImageUrl = $"{_server.Url}stories/",
                    RendererExternalImageUrls = externalUrls,
                    // A photo's 15 s budget is not what these tests are about, and a loaded CI
                    // runner has gone past it (17 s); a render that ends still ends at once.
                    RendererTimeoutMilliseconds = 60000,
                }
            ),
            _log
        );

        var drawn = await renderer.RenderAsync(jsonPath, pngPath, CancellationToken.None);

        return (drawn, pngPath);
    }

    /// <summary>Why a render was not drawn, as the renderer logged it.</summary>
    private string RendererSaid =>
        string.Join("; ", _log.AtLeast(LogLevel.Warning).Select(e => e.Message));

    private static string Render(int side, params string[] spriteNames) =>
        $$"""
            {"planes":[{"z":0,"color":0,"cornerPoints":[{"x":0,"y":0},{"x":{{side}},"y":0},{"x":0,"y":{{side}}},{"x":{{side}},"y":{{side}}}]}],
             "sprites":[{{string.Join(
                ",",
                spriteNames.Select(name => $$"""{"name":"{{name}}","x":0,"y":0,"z":1}""")
            )}}]}
            """;

    [Fact]
    public async Task AnExternalImageOutsideTheAllowedUrlsIsNotFetched()
    {
        _server.Serve("/secret.png", PIXEL);

        var (drawn, png) = await RenderAsync(Render(10, $"{_server.Url}secret.png"));

        drawn.Should().BeTrue("the picture still comes out, without the image ({0})", RendererSaid);
        File.Exists(png).Should().BeTrue();
        _server.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task AnExternalImageUnderAnAllowedUrlIsFetched()
    {
        _server.Serve("/images/pixel.png", PIXEL);

        var (drawn, _) = await RenderAsync(
            Render(10, $"{_server.Url}images/pixel.png"),
            $"{_server.Url}images/"
        );

        drawn.Should().BeTrue("the renderer logged {0}", RendererSaid);
        _server.Requests.Should().Equal("/images/pixel.png");
    }

    [Fact]
    public async Task AnAllowedUrlThatRedirectsElsewhereIsNotFollowed()
    {
        _server.Redirect("/images/moved.png", "/secret.png");
        _server.Serve("/secret.png", PIXEL);

        var (drawn, _) = await RenderAsync(
            Render(10, $"{_server.Url}images/moved.png"),
            $"{_server.Url}images/"
        );

        drawn.Should().BeTrue("the renderer logged {0}", RendererSaid);
        _server.Requests.Should().Equal("/images/moved.png");
    }

    [Fact]
    public async Task ALibraryNameThatIsAPathIsNotFetched()
    {
        var (drawn, _) = await RenderAsync(
            Render(10, "../../../outside_64_a_0_0", "chair_64_a_0_0")
        );

        drawn.Should().BeTrue("the renderer logged {0}", RendererSaid);
        _server.Requests.Should().Equal("/furni/chair.nitro");
    }

    [Fact]
    public async Task ACanvasLargerThanAPhotoIsRefused()
    {
        var (drawn, png) = await RenderAsync(Render(3000));

        drawn.Should().BeFalse();
        File.Exists(png).Should().BeFalse();
        // Refused for its size, not just not drawn: a render that ran out of time draws nothing too.
        RendererSaid.Should().Contain("canvas out of range: 3000x3000");
    }

    /// <summary>A 4x4 render on a grey background (0x808080, z 0: a sprite in front of it has a lower z) with the given sprites and lab choices.</summary>
    private static string LabRender(string sprites, string filters, int zoom = 1) =>
        $$"""
            {"planes":[{"z":0,"color":8421504,"cornerPoints":[{"x":0,"y":0},{"x":4,"y":0},{"x":0,"y":4},{"x":4,"y":4}]}],
             "sprites":[{{sprites}}],"modifiers":{},"filters":{{filters}},"roomid":1{{(
                zoom != 1 ? $",\"zoom\":{zoom}" : ""
            )}}}
            """;

    /// <summary>A PNG of one colour, <paramref name="side"/> pixels square (8-bit RGBA, unfiltered).</summary>
    private static byte[] Png(int side, uint rgba) => Png(side, side, (_, _) => rgba);

    /// <summary>A PNG <paramref name="width"/> x <paramref name="height"/>, each pixel's RGBA from <paramref name="pixel"/>.</summary>
    private static byte[] Png(int width, int height, Func<int, int, uint> pixel)
    {
        var raw = new MemoryStream();

        for (var y = 0; y < height; y++)
        {
            raw.WriteByte(0);

            for (var x = 0; x < width; x++)
            {
                var rgba = pixel(x, y);

                raw.WriteByte((byte)(rgba >> 24));
                raw.WriteByte((byte)(rgba >> 16));
                raw.WriteByte((byte)(rgba >> 8));
                raw.WriteByte((byte)rgba);
            }
        }

        var idat = new MemoryStream();

        using (var zlib = new ZLibStream(idat, CompressionLevel.Optimal, true))
            zlib.Write(raw.ToArray());

        var ihdr = new byte[13];

        ihdr[3] = (byte)width;
        ihdr[7] = (byte)height;
        ihdr[8] = 8;
        ihdr[9] = 6;

        var png = new MemoryStream();

        png.Write([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);

        foreach (
            var (type, data) in new[]
            {
                ("IHDR", ihdr),
                ("IDAT", idat.ToArray()),
                ("IEND", Array.Empty<byte>()),
            }
        )
        {
            png.Write([
                (byte)(data.Length >> 24),
                (byte)(data.Length >> 16),
                (byte)(data.Length >> 8),
                (byte)data.Length,
            ]);
            png.Write(Encoding.ASCII.GetBytes(type));
            png.Write(data);
            // The renderer reads the chunks, not their checksums.
            png.Write([0, 0, 0, 0]);
        }

        return png.ToArray();
    }

    /// <summary>A .nitro library holding one asset, <paramref name="assetName"/>, a 1x1 picture of the colour.</summary>
    private static byte[] Library(string name, string assetName, uint rgba)
    {
        var zip = new MemoryStream();

        using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, true))
        {
            void Add(string file, byte[] bytes)
            {
                using var entry = archive.CreateEntry(file).Open();

                entry.Write(bytes);
            }

            Add(
                $"{name}.json",
                Encoding.UTF8.GetBytes(
                    $$"""{"name":"{{name}}","assets":[{"name":"{{assetName}}","x":0,"y":0}]}"""
                )
            );
            Add(
                $"{name}_spritesheet.json",
                Encoding.UTF8.GetBytes(
                    "{\"frames\":{\""
                        + assetName
                        + "\":{\"frame\":{\"x\":0,\"y\":0,\"w\":1,\"h\":1}}},\"meta\":{\"image\":\""
                        + name
                        + ".png\"}}"
                )
            );
            Add($"{name}.png", Png(1, rgba));
        }

        return zip.ToArray();
    }

    /// <summary>
    /// A .nitro library of <paramref name="width"/> x <paramref name="height"/> assets drawn by
    /// <paramref name="pixel"/>, each with its own frame of the sheet, side by side; an asset with a
    /// source is that asset's picture, flipped when it says so (as the bundles name a mirrored direction).
    /// </summary>
    private static byte[] Library(
        string name,
        int width,
        int height,
        Func<int, int, uint> pixel,
        params (string Name, string? Source, bool FlipH)[] assets
    )
    {
        var drawn = assets
            .Where(asset => asset.Source == null)
            .Select(asset => asset.Name)
            .ToArray();
        var zip = new MemoryStream();

        using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, true))
        {
            void Add(string file, string text)
            {
                using var entry = archive.CreateEntry(file).Open();

                entry.Write(Encoding.UTF8.GetBytes(text));
            }

            var assetList = assets.Select(asset =>
                asset.Source == null
                    ? $"{{\"name\":\"{asset.Name}\",\"x\":0,\"y\":0}}"
                    : $"{{\"name\":\"{asset.Name}\",\"source\":\"{asset.Source}\",\"x\":0,\"y\":0,\"flipH\":{(asset.FlipH ? "true" : "false")}}}"
            );
            var frames = drawn.Select(
                (asset, i) =>
                    $"\"{asset}\":{{\"frame\":{{\"x\":{i * width},\"y\":0,\"w\":{width},\"h\":{height}}}}}"
            );

            Add(
                $"{name}.json",
                $"{{\"name\":\"{name}\",\"assets\":[{string.Join(",", assetList)}]}}"
            );
            Add(
                $"{name}_spritesheet.json",
                $"{{\"frames\":{{{string.Join(",", frames)}}},\"meta\":{{\"image\":\"{name}.png\"}}}}"
            );

            using var image = archive.CreateEntry($"{name}.png").Open();

            image.Write(Png(width * drawn.Length, height, (x, y) => pixel(x % width, y)));
        }

        return zip.ToArray();
    }

    /// <summary>The RGBA of a pixel of a PNG the renderer wrote (8-bit RGBA, every row unfiltered).</summary>
    private static uint PixelAt(string png, int x, int y)
    {
        var bytes = File.ReadAllBytes(png);
        var width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
        var idat = new MemoryStream();

        for (var pos = 8; pos < bytes.Length; )
        {
            var length =
                (bytes[pos] << 24)
                | (bytes[pos + 1] << 16)
                | (bytes[pos + 2] << 8)
                | bytes[pos + 3];

            if (Encoding.ASCII.GetString(bytes, pos + 4, 4) == "IDAT")
                idat.Write(bytes, pos + 8, length);

            pos += 12 + length;
        }

        idat.Position = 0;

        using var zlib = new ZLibStream(idat, CompressionMode.Decompress);
        var raw = new MemoryStream();

        zlib.CopyTo(raw);

        var o = (y * ((width * 4) + 1)) + 1 + (x * 4);
        var data = raw.ToArray();

        return (uint)((data[o] << 24) | (data[o + 1] << 16) | (data[o + 2] << 8) | data[o + 3]);
    }

    [Fact]
    public async Task AnAvatarsPartIsDrawnFromTheLibraryTheFigureMapNames()
    {
        _server.Serve(
            "/figuremap.json",
            Encoding.UTF8.GetBytes(
                """{"libraries":[{"id":"hh_human_shirt","parts":[{"id":215,"type":"ch"}]}]}"""
            )
        );
        _server.Serve(
            "/figures/hh_human_shirt.nitro",
            Library("hh_human_shirt", "h_std_ch_215_2_0", 0xFFFFFFFF)
        );

        // The part as AvatarImageCache sends it: its asset's name, where it lies and its colour.
        var (drawn, png) = await RenderAsync(
            LabRender("""{"name":"h_std_ch_215_2_0","x":1,"y":2,"z":-1,"color":16711680}""", "[]")
        );

        drawn.Should().BeTrue("the renderer logged {0}", RendererSaid);
        _server.Requests.Should().Contain(["/figuremap.json", "/figures/hh_human_shirt.nitro"]);
        PixelAt(png, 1, 2).Should().Be(0xFF0000FF, "the part is drawn there, tinted red");
        PixelAt(png, 0, 0).Should().Be(0x808080FF);
    }

    [Fact]
    public async Task AnAvatarEffectsSpriteIsDrawnFromTheLibraryTheEffectMapNames()
    {
        _server.Serve(
            "/effectmap.json",
            Encoding.UTF8.GetBytes("""{"effects":[{"id":"1","lib":"Spotlight","type":"fx"}]}""")
        );
        _server.Serve(
            "/effects/Spotlight.nitro",
            Library("Spotlight", "h_std_fx1_1_1_0_0", 0x00FF00FF)
        );

        var (drawn, png) = await RenderAsync(
            LabRender("""{"name":"h_std_fx1_1_1_0_0","x":3,"y":3,"z":-1}""", "[]")
        );

        drawn.Should().BeTrue("the renderer logged {0}", RendererSaid);
        _server.Requests.Should().Contain(["/effectmap.json", "/effects/Spotlight.nitro"]);
        PixelAt(png, 3, 3).Should().Be(0x00FF00FF);
    }

    [Fact]
    public async Task ThePhotoLabsColourMatrixIsAppliedAtItsStrength()
    {
        // Yellow (CameraEffect.as) takes the blue out; at alpha 255 all of it, at 0 none.
        var (drawn, png) = await RenderAsync(LabRender("", """[{"name":"Yellow","alpha":255}]"""));

        drawn.Should().BeTrue("the renderer logged {0}", RendererSaid);
        PixelAt(png, 2, 2).Should().Be(0x808000FF);

        var (_, none) = await RenderAsync(LabRender("", """[{"name":"Yellow","alpha":0}]"""));

        PixelAt(none, 2, 2).Should().Be(0x808080FF);
    }

    [Fact]
    public async Task ThePhotoLabsZoomShowsTheMiddleHalfAtTwiceTheSize()
    {
        _server.Serve(
            "/figuremap.json",
            Encoding.UTF8.GetBytes(
                """{"libraries":[{"id":"hh_human_body","parts":[{"id":1,"type":"bd"}]}]}"""
            )
        );
        _server.Serve(
            "/figures/hh_human_body.nitro",
            Library("hh_human_body", "h_std_bd_1_2_0", 0xFFFFFFFF)
        );

        var part = """{"name":"h_std_bd_1_2_0","x":2,"y":2,"z":-1}""";
        var (_, flat) = await RenderAsync(LabRender(part, "[]"));

        PixelAt(flat, 3, 3).Should().Be(0x808080FF, "unzoomed, the part is the one pixel at 2,2");

        var (drawn, zoomed) = await RenderAsync(LabRender(part, "[]", 2));

        drawn.Should().BeTrue("the renderer logged {0}", RendererSaid);
        PixelAt(zoomed, 2, 2).Should().Be(0xFFFFFFFF);
        PixelAt(zoomed, 3, 3).Should().Be(0xFFFFFFFF, "zoomed, the pixel at 2,2 covers 2,2 to 3,3");
        PixelAt(zoomed, 1, 1).Should().Be(0x808080FF);
    }

    [Fact]
    public async Task ThePhotoLabsCompositeAndFrameAreDrawnFromTheEffectPictures()
    {
        _server.Serve("/stories/pinky_nrm.png", Png(4, 0xFF000080));
        _server.Serve("/stories/frame_gold.png", Png(4, 0x0000FFFF));

        var (drawn, png) = await RenderAsync(
            LabRender("", """[{"name":"pinky_nrm","alpha":255}]""")
        );

        drawn.Should().BeTrue("the renderer logged {0}", RendererSaid);
        // pinky_nrm is drawn normally at its own half alpha over the grey.
        PixelAt(png, 0, 0).Should().Be(0xC04040FF);

        var (_, framed) = await RenderAsync(LabRender("", """[{"name":"frame_gold"}]"""));

        PixelAt(framed, 0, 0).Should().Be(0x0000FFFF);
    }

    [Fact]
    public async Task APlanesTextureColumnsAreDrawnFromTheRoomContentTintedByTheirColour()
    {
        _server.Serve(
            "/room/HabboRoomContent.nitro",
            Library("HabboRoomContent", 2, 2, (_, _) => 0x808080FF, ("wall_tex", null, false))
        );

        // A 4x4 upright plane (RoomPlane.getDrawingDatas): one column of the 2x2 texture, tinted red.
        var (drawn, png) = await RenderAsync(
            """
            {"planes":[{"z":2,"color":0,"cornerPoints":[{"x":4,"y":4},{"x":0,"y":4},{"x":4,"y":0},{"x":0,"y":0}]},
              {"z":1,"color":16711680,"bottomAligned":false,"texCols":[{"assetNames":["wall_tex"]}],
               "cornerPoints":[{"x":4,"y":4},{"x":0,"y":4},{"x":4,"y":0},{"x":0,"y":0}]}],
             "sprites":[]}
            """
        );

        drawn.Should().BeTrue("the renderer logged {0}", RendererSaid);
        _server.Requests.Should().Contain("/room/HabboRoomContent.nitro");
        PixelAt(png, 0, 0).Should().Be(0x800000FF, "the texture's grey times the plane's red");
        PixelAt(png, 3, 3)
            .Should()
            .Be(0x800000FF, "the column repeats across the plane and its cell down it");
    }

    [Fact]
    public async Task AFloorIsReadAtEachPixelsCentreAsTheOfficialPhotoIs()
    {
        // A 2x2 texture: red, green / blue, white.
        _server.Serve(
            "/room/HabboRoomContent.nitro",
            Library(
                "HabboRoomContent",
                2,
                2,
                (x, y) =>
                    (x, y) switch
                    {
                        (0, 0) => 0xFF0000FF,
                        (1, 0) => 0x00FF00FF,
                        (0, 1) => 0x0000FFFF,
                        _ => 0xFFFFFFFF,
                    },
                ("floor_tex", null, false)
            )
        );

        // A 2:1 floor diamond, its top corner at 4,0: the pixel at 3,1 has its centre over texture pixel 1,1.
        var (drawn, png) = await RenderAsync(
            """
            {"planes":[{"z":2,"color":0,"cornerPoints":[{"x":8,"y":4},{"x":0,"y":4},{"x":8,"y":0},{"x":0,"y":0}]},
              {"z":1,"color":16777215,"bottomAligned":false,"texCols":[{"assetNames":["floor_tex"]}],
               "cornerPoints":[{"x":4,"y":4},{"x":0,"y":2},{"x":8,"y":2},{"x":4,"y":0}]}],
             "sprites":[]}
            """
        );

        drawn.Should().BeTrue("the renderer logged {0}", RendererSaid);
        PixelAt(png, 4, 0)
            .Should()
            .Be(0xFF0000FF, "the top corner shows the texture's first pixel");
        PixelAt(png, 3, 1)
            .Should()
            .Be(0xFFFFFFFF, "read at the pixel's centre, one pixel along each side");
    }

    [Fact]
    public async Task APlanesMaskCutsItsShapeOutOfThePlane()
    {
        // A door mask as the room content has it: black where the hole is, white around it.
        _server.Serve(
            "/room/HabboRoomContent.nitro",
            Library(
                "HabboRoomContent",
                2,
                2,
                (x, _) => x == 0 ? 0x000000FF : 0xFFFFFFFF,
                ("door_64", null, false)
            )
        );

        var (drawn, png) = await RenderAsync(
            """
            {"planes":[{"z":2,"color":255,"cornerPoints":[{"x":4,"y":4},{"x":0,"y":4},{"x":4,"y":0},{"x":0,"y":0}]},
              {"z":1,"color":16777215,"bottomAligned":false,"texCols":[],
               "masks":[{"name":"door_64","location":{"x":1,"y":1},"flipH":false,"flipV":false}],
               "cornerPoints":[{"x":4,"y":4},{"x":0,"y":4},{"x":4,"y":0},{"x":0,"y":0}]}],
             "sprites":[]}
            """
        );

        drawn.Should().BeTrue("the renderer logged {0}", RendererSaid);
        PixelAt(png, 1, 1).Should().Be(0x0000FFFF, "the hole shows the background behind the wall");
        PixelAt(png, 2, 1).Should().Be(0xFFFFFFFF, "the mask's white keeps the wall");
        PixelAt(png, 3, 3).Should().Be(0xFFFFFFFF);
    }

    [Fact]
    public async Task AFlippedSpriteIsItsAssetMirroredOnceFromItsX()
    {
        // A chair's direction 4 named as its mirrored direction 2 (an asset with a source and flipH):
        // its left pixel red, its right blue.
        _server.Serve(
            "/furni/chair.nitro",
            Library(
                "chair",
                2,
                1,
                (x, _) => x == 0 ? 0xFF0000FF : 0x0000FFFF,
                ("chair_64_a_2_0", null, false),
                ("chair_64_a_4_0", "chair_64_a_2_0", true)
            )
        );

        // As Flash's canvas sends it: the sprite flipped, x where its mirrored picture starts.
        var (drawn, png) = await RenderAsync(
            LabRender("""{"name":"chair_64_a_4_0","x":1,"y":1,"z":-1,"flipH":true}""", "[]")
        );

        drawn.Should().BeTrue("the renderer logged {0}", RendererSaid);
        PixelAt(png, 1, 1).Should().Be(0x0000FFFF, "mirrored once, the blue pixel comes first");
        PixelAt(png, 2, 1).Should().Be(0xFF0000FF);
        PixelAt(png, 0, 1).Should().Be(0x808080FF, "nothing is drawn left of its x");
    }

    [Fact]
    public async Task ALabEffectTheRendererDoesNotKnowFetchesNothing()
    {
        var (drawn, png) = await RenderAsync(
            LabRender("", """[{"name":"../../secret","alpha":255}]""")
        );

        drawn.Should().BeTrue("the picture still comes out ({0})", RendererSaid);
        _server.Requests.Should().BeEmpty();
        PixelAt(png, 0, 0).Should().Be(0x808080FF);
    }

    /// <summary>
    /// A loopback HTTP server that records every path asked for: a served file, a redirect, or 404.
    /// Raw sockets rather than <c>HttpListener</c>, which needs a URL reservation on Windows.
    /// </summary>
    private sealed class LocalHttpServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly ConcurrentDictionary<string, byte[]> _files = new();
        private readonly ConcurrentDictionary<string, string> _redirects = new();
        private readonly ConcurrentQueue<string> _requests = new();
        private readonly CancellationTokenSource _stop = new();

        public LocalHttpServer()
        {
            _listener.Start();
            _ = AcceptAsync();
        }

        public string Url => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/";

        public IReadOnlyList<string> Requests => [.. _requests];

        public void Serve(string path, byte[] body) => _files[path] = body;

        public void Redirect(string path, string to) => _redirects[path] = to;

        private async Task AcceptAsync()
        {
            while (!_stop.IsCancellationRequested)
            {
                TcpClient client;

                try
                {
                    client = await _listener.AcceptTcpClientAsync(_stop.Token);
                }
                catch (Exception)
                {
                    return;
                }

                _ = AnswerAsync(client);
            }
        }

        private async Task AnswerAsync(TcpClient client)
        {
            using var _ = client;
            var stream = client.GetStream();
            var request = new StringBuilder();
            var buffer = new byte[4096];

            while (!request.ToString().Contains("\r\n\r\n"))
            {
                var read = await stream.ReadAsync(buffer);

                if (read == 0)
                    return;

                request.Append(Encoding.ASCII.GetString(buffer, 0, read));
            }

            var path = request.ToString().Split(' ')[1];

            _requests.Enqueue(path);

            byte[] head;
            var body = Array.Empty<byte>();

            if (_redirects.TryGetValue(path, out var to))
                head = Encoding.ASCII.GetBytes(
                    $"HTTP/1.1 302 Found\r\nLocation: {to}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
                );
            else if (_files.TryGetValue(path, out var file))
            {
                body = file;
                head = Encoding.ASCII.GetBytes(
                    $"HTTP/1.1 200 OK\r\nContent-Type: image/png\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n"
                );
            }
            else
                head = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
                );

            await stream.WriteAsync(head);
            await stream.WriteAsync(body);
        }

        public void Dispose()
        {
            _stop.Cancel();
            _listener.Stop();
        }
    }
}
