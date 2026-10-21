using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Database.Entities.Moderation;
using Turbo.Database.Entities.Players;
using Turbo.Operations;
using Turbo.Operations.Configuration;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The panel's word filter (<c>/api/word-filter</c>): a word added there is filtered out of what
/// players type at once, and one taken off is not; words that could never match are refused with
/// why; only staff who may change the filter change it, and each change is in the command log.
/// </summary>
public sealed class WordFilterEndpointsTests : IAsyncDisposable
{
    private const int STAFF = 1;

    private readonly Fakes _fakes = new();
    private readonly SqliteDb _db = new();
    private readonly HttpClient _client = new();
    private readonly int _port;
    private readonly IHostedService _server;
    private readonly WordFilter _filter;

    /// <summary>The nodes the staff member holds.</summary>
    private Func<string, bool> _holds = _ => true;

    public WordFilterEndpointsTests()
    {
        _port = TestPorts.Free();

        _fakes.Handlers["GetSessionAsync"] = _ =>
            Task.FromResult<AdminSessionSnapshot?>(
                new AdminSessionSnapshot
                {
                    PlayerId = STAFF,
                    ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                }
            );
        _fakes.Handlers["HasAsync"] = call => Task.FromResult(_holds((string)call.Args[0]!));
        _fakes.Handlers["CreateDbContextAsync"] = _ => _db.CreateDbContextAsync();

        // The hotel's own filter, as chat and the messenger use it.
        _filter = new WordFilter(
            _db,
            Options.Create(new OperationsConfig { WordFilterReplacement = "bobba" }),
            NullLogger<IWordFilter>.Instance
        );
        _fakes.Instances[(typeof(IWordFilter), null)] = _filter;

        _db.Insert(
            new PlayerEntity
            {
                Id = STAFF,
                Name = "Staff",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
        _db.Insert(new FilterWordEntity { Id = 1, Word = "darn" });

        (_server, _, _) = AdminApiServerTests.Build(
            $"http://127.0.0.1:{_port}",
            _fakes,
            NullLoggerFactory.Instance
        );
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _server.StopAsync(CancellationToken.None);
        _db.Dispose();
    }

    [Fact]
    public async Task A_word_added_is_filtered_at_once_and_one_taken_off_no_longer_is()
    {
        await _filter.ReloadAsync(Ct);
        await _server.StartAsync(Ct);
        _filter.Filter("well heck darn").Should().Be("well heck bobba");

        var added = await JsonAsync(
            await SendAsync(HttpMethod.Post, "/api/word-filter", new { words = "Heck, darn" })
        );

        added
            .GetProperty("added")
            .EnumerateArray()
            .Select(x => x.GetString())
            .Should()
            .Equal("heck");
        added
            .GetProperty("existing")
            .EnumerateArray()
            .Select(x => x.GetString())
            .Should()
            .Equal("darn");
        _filter.Filter("well HECK darn").Should().Be("well bobba bobba");

        (await SendAsync(HttpMethod.Delete, "/api/word-filter/1"))
            .StatusCode.Should()
            .Be(HttpStatusCode.NoContent);
        _filter.Filter("well heck darn").Should().Be("well bobba darn");

        var list = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/word-filter"));
        list.GetProperty("replacement").GetString().Should().Be("bobba");
        list.GetProperty("canManage").GetBoolean().Should().BeTrue();
        list.GetProperty("words")
            .EnumerateArray()
            .Select(x => x.GetProperty("word").GetString())
            .Should()
            .Equal("heck");

        var test = await JsonAsync(
            await SendAsync(HttpMethod.Post, "/api/word-filter/test", new { text = "oh heck" })
        );
        test.GetProperty("shown").GetString().Should().Be("oh bobba");
        test.GetProperty("filtered").GetBoolean().Should().BeTrue();

        await using var db = await _db.CreateDbContextAsync(Ct);
        db.CommandLogs.Select(x => x.Command + " " + x.Arguments)
            .Should()
            .Equal("wordfilter add heck", "wordfilter remove darn");
    }

    [Fact]
    public async Task A_word_that_could_never_match_is_refused_with_why()
    {
        await _server.StartAsync(Ct);

        var added = await JsonAsync(
            await SendAsync(HttpMethod.Post, "/api/word-filter", new { words = "f.u ok" })
        );

        added.GetProperty("added").EnumerateArray().Select(x => x.GetString()).Should().Equal("ok");
        var refused = added.GetProperty("refused").EnumerateArray().Single();
        refused.GetProperty("word").GetString().Should().Be("f.u");
        refused.GetProperty("reason").GetString().Should().Contain("letters and digits");
    }

    [Fact]
    public async Task Staff_who_may_not_change_the_filter_only_read_it()
    {
        _holds = node => node != PermissionNodes.Moderation.FILTER_MANAGE;
        await _server.StartAsync(Ct);

        (await JsonAsync(await SendAsync(HttpMethod.Get, "/api/word-filter")))
            .GetProperty("canManage")
            .GetBoolean()
            .Should()
            .BeFalse();
        var refused = await SendAsync(HttpMethod.Post, "/api/word-filter", new { words = "heck" });
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await JsonAsync(refused))
            .GetProperty("message")
            .GetString()
            .Should()
            .Be("You can't change the word filter.");
        // Trying a line is not a GET but changes nothing: reading the filter is enough.
        (await SendAsync(HttpMethod.Post, "/api/word-filter/test", new { text = "oh heck" }))
            .StatusCode.Should()
            .Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Delete, "/api/word-filter/1"))
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);

        _holds = node => node != PermissionNodes.Moderation.TOOL;

        (await SendAsync(HttpMethod.Get, "/api/word-filter"))
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        object? body = null
    )
    {
        var request = new HttpRequestMessage(method, $"http://127.0.0.1:{_port}{path}");

        request.Headers.Authorization = new("Bearer", "a-session");

        if (body is not null)
            request.Content = JsonContent.Create(body);

        return await _client.SendAsync(request, Ct);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;
}
