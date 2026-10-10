using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.PacketHandlers.Camera;
using Xunit;

namespace Turbo.Tests.PacketHandlers;

/// <summary>
/// The render data a camera sends is the client's, and the renderer (<c>render.mjs</c>, run by
/// <see cref="CameraRenderer"/>) must not let it steer the server: an external image is fetched only
/// from a url the hotel allowed (never redirected elsewhere), a furniture library name cannot climb
/// out of the bundle url, and a canvas larger than a photo is refused instead of allocated.
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
                    RendererExternalImageUrls = externalUrls,
                }
            ),
            NullLogger<CameraRenderer>.Instance
        );

        var drawn = await renderer.RenderAsync(jsonPath, pngPath, CancellationToken.None);

        return (drawn, pngPath);
    }

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

        drawn.Should().BeTrue("the picture still comes out, without the image");
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

        drawn.Should().BeTrue();
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

        drawn.Should().BeTrue();
        _server.Requests.Should().Equal("/images/moved.png");
    }

    [Fact]
    public async Task ALibraryNameThatIsAPathIsNotFetched()
    {
        var (drawn, _) = await RenderAsync(
            Render(10, "../../../outside_64_a_0_0", "chair_64_a_0_0")
        );

        drawn.Should().BeTrue();
        _server.Requests.Should().Equal("/furni/chair.nitro");
    }

    [Fact]
    public async Task ACanvasLargerThanAPhotoIsRefused()
    {
        var (drawn, png) = await RenderAsync(Render(3000));

        drawn.Should().BeFalse();
        File.Exists(png).Should().BeFalse();
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
