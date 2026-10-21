using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Habbo;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Gamedata;

/// <summary>
/// A check reads Habbo's <c>/gamedata/hashes</c> before its files, and each version it keeps holds
/// the hash Habbo listed that file under, which the Sources tab compares with what the hotel took
/// in. A list that can't be read leaves the hashes as they were, and the check goes on.
/// </summary>
public sealed class HabboGamedataHashesTests : IDisposable
{
    private const string GAMEDATA = "https://www.habbo.com/gamedata/";

    private readonly SqliteDb _db = new();
    private readonly Host _habbo = new();

    public HabboGamedataHashesTests()
    {
        _habbo.Serve(
            "external_variables/0",
            "flash.client.url=//images.habbo.com/gordon/flash-assets-PRODUCTION-1/\n"
        );
        _habbo.Serve(
            "furnidata_json/0",
            """{"roomitemtypes":{"furnitype":[]},"wallitemtypes":{"furnitype":[]}}"""
        );
        _habbo.Serve("external_flash_texts/0", "a=1\n");
        _habbo.Serve("productdata_json/0", """{"productdata":{"product":[]}}""");
        _habbo.Serve(
            "figuredata/0",
            """<figuredata><colors><palette id="1"><color id="1" index="1" club="0" selectable="1">DDDDDD</color></palette></colors><sets></sets></figuredata>"""
        );
        Hashes(texts: "texts-1");
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Each_version_a_check_keeps_holds_the_hash_habbo_lists_its_file_under()
    {
        var first = await Releases().CheckAsync(Ct);

        _habbo.Asked.First().Should().Be(GAMEDATA + "hashes");
        first.Release.HabboHash.Should().Be("furni-1");
        first.Products!.HabboHash.Should().Be("products-1");
        first.Texts!.HabboHash.Should().Be("texts-1");
        first.Variables!.HabboHash.Should().Be("variables-1");
        first.Figures!.HabboHash.Should().Be("figures-1");

        // Habbo changes its texts: a new version under the new hash; the furniture data keeps its own.
        _habbo.Serve("external_flash_texts/0", "a=2\n");
        Hashes(texts: "texts-2");

        var second = await Releases().CheckAsync(Ct);

        second.TextsAreNew.Should().BeTrue();
        second.Texts!.HabboHash.Should().Be("texts-2");
        second.Release.HabboHash.Should().Be("furni-1");

        // Habbo's filter refuses the list: the check goes on and every hash stays as it was.
        _habbo.Serve("hashes", "<html>refused</html>");

        var third = await Releases().CheckAsync(Ct);

        third.Texts!.HabboHash.Should().Be("texts-2");
        third.Figures!.HabboHash.Should().Be("figures-1");
    }

    private void Hashes(string texts) =>
        _habbo.Serve(
            "hashes",
            $$"""
            {"hashes":[
              {"name":"furnidata","url":"{{GAMEDATA}}furnidata_xml","hash":"furni-1"},
              {"name":"productdata","url":"{{GAMEDATA}}productdata_xml","hash":"products-1"},
              {"name":"external_variables","url":"{{GAMEDATA}}external_variables","hash":"variables-1"},
              {"name":"external_texts","url":"{{GAMEDATA}}external_flash_texts","hash":"{{texts}}"},
              {"name":"figurepartlist","url":"{{GAMEDATA}}figuredata","hash":"figures-1"}
            ]}
            """
        );

    private HabboReleaseService Releases() =>
        new(
            _db,
            Options.Create(new GamedataConfig()),
            new HabboGamedataClient(_habbo, NullLogger<HabboGamedataClient>.Instance),
            TimeProvider.System,
            NullLogger<HabboReleaseService>.Instance
        );

    /// <summary>www.habbo.com's gamedata: what it serves by path, 404 for the rest.</summary>
    private sealed class Host : HttpMessageHandler, IHttpClientFactory
    {
        private readonly ConcurrentDictionary<string, byte[]> _files = new();

        public ConcurrentQueue<string> Asked { get; } = new();

        public void Serve(string path, string text) =>
            _files[GAMEDATA + path] = Encoding.UTF8.GetBytes(text);

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var url = request.RequestUri!.ToString();

            Asked.Enqueue(url);

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
