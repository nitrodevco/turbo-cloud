using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using Turbo.Admin.Assets;
using Turbo.Admin.Configuration;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The panel's windows browser reads the client's <c>skins.json</c> and <c>template-index.json</c>
/// where the browser finds them: beside the client's page, kept until a new client could have
/// changed them or the panel asks again.
/// </summary>
public sealed class ClientWindowsTests
{
    private readonly Host _host = new();
    private readonly ManualTimeProvider _clock = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_file_is_read_beside_the_client_page_and_kept_until_it_could_have_changed()
    {
        _host.Serve("https://hotel.example/play/skins.json", """{"version":1}""");
        _host.Serve("https://client.example/nitro/template-index.json", """{"templates":{}}""");

        // The login page's folder (its query is the ticket's), and a preview page that is a folder.
        var login = Windows(
            new AdminConfig { ClientLoginUrl = "https://hotel.example/play/client?sso={ticket}" }
        );
        var preview = Windows(
            new AdminConfig { ClientPreviewUrl = "https://client.example/nitro/" }
        );

        (await login.GetAsync(ClientWindows.SKINS, false, Ct))
            .Should()
            .Match<ClientWindowsFile>(x =>
                x.Url == "https://hotel.example/play/skins.json"
                && Encoding.UTF8.GetString(x.Json!) == """{"version":1}"""
            );
        (await preview.GetAsync(ClientWindows.INDEX, false, Ct)).Json.Should().NotBeNull();

        await login.GetAsync(ClientWindows.SKINS, false, Ct);
        _host.Requests.Should().HaveCount(2, "a kept file is not read again");

        _clock.Advance(ClientWindows.KEEP);
        await login.GetAsync(ClientWindows.SKINS, false, Ct);
        await login.GetAsync(ClientWindows.SKINS, true, Ct);
        _host
            .Requests.Should()
            .HaveCount(4, "an old copy, and one asked for fresh, are read again");
    }

    [Fact]
    public async Task Without_a_client_page_or_a_file_the_reason_is_given()
    {
        (await Windows(new AdminConfig()).GetAsync(ClientWindows.SKINS, false, Ct))
            .Problem.Should()
            .Contain("ClientPreviewUrl");

        var missing = await Windows(
                new AdminConfig { ClientPreviewUrl = "https://client.example/" }
            )
            .GetAsync(ClientWindows.INDEX, false, Ct);

        missing.Json.Should().BeNull();
        missing
            .Problem.Should()
            .Contain("404")
            .And.Contain("https://client.example/template-index.json");
    }

    private ClientWindows Windows(AdminConfig config) =>
        new(
            _host,
            new TestOptionsMonitor<AdminConfig>(config),
            _clock,
            new CapturingLogger<ClientWindows>()
        );

    /// <summary>The client's host: what it serves by address, 404 for the rest.</summary>
    private sealed class Host : HttpMessageHandler, IHttpClientFactory
    {
        private readonly ConcurrentDictionary<string, byte[]> _files = new();

        public ConcurrentQueue<string> Requests { get; } = new();

        public void Serve(string url, string text) => _files[url] = Encoding.UTF8.GetBytes(text);

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
