using System.Collections.Immutable;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// How many times an admin request asks a player's permission grain. Each declared node was a
/// grain call of its own on top of the session's <c>admin.panel</c> check, and <c>/me</c> asked
/// fifteen nodes one after another: the session now reads the player's resolved permissions
/// once, and every declaration and handler answers from that.
/// </summary>
public sealed class AdminPermissionCallsTests : IAsyncDisposable
{
    private readonly Fakes _fakes = new();
    private readonly SqliteDb _db = new();
    private readonly HttpClient _client = new();
    private readonly int _port = TestPorts.Free();
    private readonly IHostedService _server;

    public AdminPermissionCallsTests()
    {
        ImmutableHashSet<string> all =
        [
            .. new CorePermissionNodeSource().Nodes.Select(x => x.Node),
        ];

        _fakes.Handlers["GetSessionAsync"] = _ =>
            Task.FromResult<AdminSessionSnapshot?>(
                new AdminSessionSnapshot
                {
                    PlayerId = 1,
                    ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                }
            );
        _fakes.Handlers["HasAsync"] = call => Task.FromResult(all.Contains((string)call.Args[0]!));
        _fakes.Handlers["GetResolvedAsync"] = _ =>
            Task.FromResult(ResolvedPermissionsSnapshot.EMPTY with { Granted = all });
        _fakes.Handlers["CreateDbContextAsync"] = _ => _db.CreateDbContextAsync();

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

    [Theory]
    [InlineData("GET", "/api/me", null)]
    [InlineData("GET", "/api/word-filter", null)]
    [InlineData("POST", "/api/word-filter/test", "{\"text\":\"hi\"}")]
    [InlineData("GET", "/api/content/achievements", null)]
    [InlineData("GET", "/api/settings", null)]
    public async Task A_request_reads_the_players_permissions_once(
        string method,
        string path,
        string? body
    )
    {
        await _server.StartAsync(Ct);

        var response = await SendAsync(new HttpMethod(method), path, body);

        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        _fakes
            .Log.Calls.Count(x => x.Method is "HasAsync" or "GetResolvedAsync")
            .Should()
            .Be(1, "{0} {1}", method, path);
    }

    // Verbose permission logging (:perm verbose) logs each check the grain answers: a node it
    // watches is still asked of the grain, so the panel's checks of it still show in that log.
    [Fact]
    public async Task A_node_watched_by_verbose_logging_is_still_asked_of_the_grain()
    {
        var all = new CorePermissionNodeSource().Nodes.Select(x => x.Node).ToImmutableHashSet();
        _fakes.Handlers["GetResolvedAsync"] = _ =>
            Task.FromResult(
                ResolvedPermissionsSnapshot.EMPTY with
                {
                    Granted = all,
                    VerboseFilter = "admin.panel",
                }
            );
        await _server.StartAsync(Ct);

        await SendAsync(HttpMethod.Get, "/api/word-filter", null);

        _fakes
            .Log.Of("HasAsync")
            .Select(x => (string)x.Args[0]!)
            .Should()
            .Equal(PermissionNodes.Admin.PANEL);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? body)
    {
        var request = new HttpRequestMessage(method, $"http://127.0.0.1:{_port}{path}");

        if (body is not null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        request.Headers.Authorization = new("Bearer", "a-session");

        return await _client.SendAsync(request, Ct);
    }
}
