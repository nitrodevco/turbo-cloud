using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Habbo.Client;
using Turbo.Tests.Assets;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Gamedata;

/// <summary>
/// Habbo's HTML client taken as nitro-studio takes it: each environment's clienturls names a
/// release, which is downloaded once, checked against its listing and its own manifest, kept, and
/// built from when its environment is followed. A release that is not what its listing says is
/// not kept, and says why.
/// </summary>
public sealed class HabboClientServiceTests : IDisposable
{
    private const string LISTING =
        "https://images.habbo.com/habbo-clients/classic-electron/win/prod/95_x/native-release.json";
    private const string ASAR =
        "https://images.habbo.com/habbo-clients/classic-electron/win/prod/95_x/app.asar";

    private readonly SqliteDb _db = new();
    private readonly AssetFolder _folder;
    private readonly Host _habbo = new();

    public HabboClientServiceTests() => _folder = new AssetFolder(_db);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _folder.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task A_release_an_environment_serves_is_downloaded_checked_and_followed()
    {
        var hab = "HAB\0 a library"u8.ToArray();
        var asar = TestClientRelease.Asar(
            (
                "release-manifest.json",
                TestClientRelease.Manifest("95_classic-js-x", ("generated/habbo-chat.hab", hab))
            ),
            ("generated/habbo-chat.hab", hab),
            ("habbo-air/launcher-build-info.json", """{"builtAtMs":1760000000000}"""u8.ToArray())
        );

        // Production serves no HTML client ("0" and no address); the sandbox does.
        _habbo.Serve(
            "https://www.habbo.com/gamedata/clienturls",
            """{"html-windows-version":"0"}"""
        );
        _habbo.Serve(
            "https://sandbox.habbo.com/gamedata/clienturls",
            $$"""{"html-windows-version":"95_classic-js-x","html-windows":"{{LISTING}}"}"""
        );
        _habbo.Serve(LISTING, TestClientRelease.Listing(asar));
        _habbo.Serve(ASAR, asar);

        var client = Client();
        var status = await client.CheckAsync(wait: true, Ct);

        status
            .Environments.Select(x => (x.Environment, x.LiveVersion, x.Version, x.Error))
            .Should()
            .Equal(
                ("production", null, null, null),
                ("sandbox", "95_classic-js-x", "95_classic-js-x", null)
            );
        status.Follow.Should().Be("sandbox");

        var release = status.Releases.Should().ContainSingle().Subject;

        release.Libraries.Should().Equal("generated/habbo-chat.hab");
        release.Environments.Should().Equal("sandbox");
        release.BuiltAt.Should().Be(DateTime.UnixEpoch.AddMilliseconds(1760000000000));

        var followed = await client.OpenFollowedAsync(Ct);

        followed!.Read("generated/habbo-chat.hab").Should().Equal(hab);
        followed
            .FileHashes["generated/habbo-chat.hab"]
            .Should()
            .Be(Convert.ToHexStringLower(SHA256.HashData(hab)));

        // Asked again, nothing is downloaded again.
        _habbo.Requests.Clear();
        await client.CheckAsync(wait: true, Ct);
        _habbo.Requests.Should().NotContain(ASAR);
    }

    [Fact]
    public async Task A_release_that_is_not_what_its_listing_says_is_not_kept_and_says_why()
    {
        var asar = TestClientRelease.Asar(
            ("release-manifest.json", TestClientRelease.Manifest("95_classic-js-x"))
        );

        _habbo.Serve(
            "https://sandbox.habbo.com/gamedata/clienturls",
            $$"""{"html-windows-version":"95_classic-js-x","html-windows":"{{LISTING}}"}"""
        );
        // The listing's hash is another file's.
        _habbo.Serve(
            LISTING,
            TestClientRelease.Listing(
                asar,
                sha256: Convert.ToHexStringLower(SHA256.HashData("other"u8.ToArray()))
            )
        );
        _habbo.Serve(ASAR, asar);

        var status = await Client().CheckAsync(wait: true, Ct);

        status.Releases.Should().BeEmpty();
        status.Follow.Should().BeNull();

        var sandbox = status.Environments.Single(x => x.Environment == "sandbox");

        sandbox.Version.Should().BeNull();
        sandbox.DownloadError.Should().Contain("SHA-256");
        // Production did not answer at all: said, and the rest goes on.
        status.Environments.Single(x => x.Environment == "production").Error.Should().NotBeNull();
        Directory
            .EnumerateDirectories(Path.Combine(_folder.Store.Root, ".habbo-client", "releases"))
            .Should()
            .BeEmpty();
    }

    private HabboClientService Client() =>
        new(
            _habbo,
            _folder.Store,
            Options.Create(new GamedataConfig()),
            TimeProvider.System,
            NullLogger<HabboClientService>.Instance
        );

    /// <summary>Habbo's hosts: what they serve by address, 404 for the rest.</summary>
    private sealed class Host : HttpMessageHandler, IHttpClientFactory
    {
        private readonly ConcurrentDictionary<string, byte[]> _files = new();

        public ConcurrentQueue<string> Requests { get; } = new();

        public void Serve(string url, string text) => _files[url] = Encoding.UTF8.GetBytes(text);

        public void Serve(string url, byte[] data) => _files[url] = data;

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var url = request.RequestUri!.ToString();

            Requests.Enqueue(url);

            return Task.FromResult(
                _files.TryGetValue(url, out var data)
                    ? new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(data),
                    }
                    : new HttpResponseMessage(HttpStatusCode.NotFound)
            );
        }
    }
}
