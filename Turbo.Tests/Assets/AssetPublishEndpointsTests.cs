using System.Collections.Immutable;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Tests.Admin;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Assets;

/// <summary>
/// The panel's publish targets (<c>/api/assets/targets</c>) over the real publish service: a target
/// that could not work is refused with why, a saved one is listed with its protocol by name and
/// never its password, a publish answers the job as the panel reads it, and its history names who
/// started it. Only staff who may change the gamedata change anything.
/// </summary>
public sealed class AssetPublishEndpointsTests : IAsyncDisposable
{
    private readonly Fakes _fakes = new();
    private readonly AssetPublishHotel _hotel = new();
    private readonly HttpClient _client = new();
    private readonly int _port;
    private readonly IHostedService _server;

    /// <summary>The nodes the staff member holds.</summary>
    private Func<string, bool> _holds = _ => true;

    public AssetPublishEndpointsTests()
    {
        using (var free = new TcpListener(IPAddress.Loopback, 0))
        {
            free.Start();
            _port = ((IPEndPoint)free.LocalEndpoint).Port;
        }

        _fakes.Handlers["GetSessionAsync"] = _ =>
            Task.FromResult<AdminSessionSnapshot?>(
                new AdminSessionSnapshot
                {
                    PlayerId = AssetPublishHotel.STAFF.Value,
                    ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                }
            );
        _fakes.Handlers["HasAsync"] = call => Task.FromResult(_holds((string)call.Args[0]!));
        _fakes.Handlers["GetPlayerNamesAsync"] = call =>
            Task.FromResult(
                ((List<PlayerId>)call.Args[0]!).ToImmutableDictionary(
                    x => x,
                    x => $"staff{x.Value}"
                )
            );
        _fakes.Instances[(typeof(IAssetPublishService), null)] = _hotel.Publishing;

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
        _hotel.Dispose();
    }

    [Fact]
    public async Task A_target_saved_is_listed_by_protocol_name_without_its_password()
    {
        await _server.StartAsync(Ct);

        var created = await SendAsync(
            HttpMethod.Post,
            "/api/assets/targets",
            Target("Live", "sftp", "files.example.com", "/srv/assets", password: "hunter2")
        );

        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await created.Content.ReadAsStringAsync(Ct);
        body.Should().NotContain("hunter2");
        var target = JsonDocument.Parse(body).RootElement;
        target.GetProperty("protocol").GetString().Should().Be("sftp");
        target.GetProperty("hasPassword").GetBoolean().Should().BeTrue();
        target.TryGetProperty("password", out _).Should().BeFalse();
        target.GetProperty("lastPublish").ValueKind.Should().Be(JsonValueKind.Null);

        var list = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/assets/targets"));
        var listed = list.GetProperty("items").EnumerateArray().Single();
        listed.GetProperty("name").GetString().Should().Be("Live");
        listed.GetProperty("host").GetString().Should().Be("files.example.com");
        listed.GetProperty("pending").GetInt32().Should().Be(0);

        var id = listed.GetProperty("id").GetInt32();
        var kept = await JsonAsync(
            await SendAsync(
                HttpMethod.Put,
                $"/api/assets/targets/{id}",
                Target("Live 2", "sftp", "files.example.com", "/srv/assets", password: null)
            )
        );
        kept.GetProperty("name").GetString().Should().Be("Live 2");
        kept.GetProperty("hasPassword").GetBoolean().Should().BeTrue();

        (await SendAsync(HttpMethod.Post, $"/api/assets/targets/{id}/forget-host-key"))
            .StatusCode.Should()
            .Be(HttpStatusCode.NoContent);
        (await SendAsync(HttpMethod.Delete, $"/api/assets/targets/{id}"))
            .StatusCode.Should()
            .Be(HttpStatusCode.NoContent);
        (await SendAsync(HttpMethod.Get, $"/api/assets/targets/{id}/history"))
            .StatusCode.Should()
            .Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("", "folder", "", "/web")]
    [InlineData("Live", "gopher", "host", "/web")]
    [InlineData("Live", "1", "host", "/web")]
    [InlineData("Live", "ftp", "", "/web")]
    [InlineData("Live", "folder", "", "relative/web")]
    public async Task A_target_that_could_not_work_is_refused_with_why(
        string name,
        string protocol,
        string host,
        string remotePath
    )
    {
        await _server.StartAsync(Ct);

        var refused = await SendAsync(
            HttpMethod.Post,
            "/api/assets/targets",
            Target(name, protocol, host, remotePath)
        );

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await JsonAsync(refused)).GetProperty("message").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task A_publish_answers_its_job_and_its_history_names_who_started_it()
    {
        await _server.StartAsync(Ct);
        await _hotel.PutBundleAsync("chair", [1, 2, 3], Ct);
        var target = await _hotel.AddFolderTargetAsync(Ct);

        var started = await SendAsync(
            HttpMethod.Post,
            $"/api/assets/targets/{target.Id}/publish",
            new { dryRun = false, deleteRemoved = false }
        );

        started.StatusCode.Should().Be(HttpStatusCode.OK);
        var job = await JsonAsync(started);
        job.GetProperty("kind").GetString().Should().Be("publish");
        job.GetProperty("status").GetString().Should().Be("running");
        job.GetProperty("title").GetString().Should().Be("Publish to Web root");
        job.GetProperty("log").ValueKind.Should().Be(JsonValueKind.Array);
        job.GetProperty("playerId").GetInt32().Should().Be(AssetPublishHotel.STAFF.Value);

        (await _hotel.EndOfJobAsync(Ct)).Result.Should().StartWith("1 sent");

        var history = await JsonAsync(
            await SendAsync(HttpMethod.Get, $"/api/assets/targets/{target.Id}/history")
        );
        var entry = history.GetProperty("items").EnumerateArray().Single();
        entry.GetProperty("uploaded").GetInt32().Should().Be(1);
        entry
            .GetProperty("playerName")
            .GetString()
            .Should()
            .Be($"staff{AssetPublishHotel.STAFF.Value}");

        var list = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/assets/targets"));
        list.GetProperty("items")[0]
            .GetProperty("lastPublish")
            .GetProperty("id")
            .GetInt32()
            .Should()
            .Be(entry.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Staff_who_may_only_see_the_gamedata_list_targets_but_change_none()
    {
        _holds = node => node != PermissionNodes.Gamedata.MANAGE;
        await _server.StartAsync(Ct);
        var target = await _hotel.AddFolderTargetAsync(Ct);

        (await SendAsync(HttpMethod.Get, "/api/assets/targets"))
            .StatusCode.Should()
            .Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Post, $"/api/assets/targets/{target.Id}/publish", new { }))
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Delete, $"/api/assets/targets/{target.Id}"))
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
        _hotel.Jobs.Current.Should().BeNull();

        _holds = node => node == PermissionNodes.Admin.PANEL;

        (await SendAsync(HttpMethod.Get, "/api/assets/targets"))
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

    private static object Target(
        string name,
        string protocol,
        string host,
        string remotePath,
        string? password = null
    ) =>
        new
        {
            name,
            protocol,
            host,
            port = 0,
            user = "assets",
            password,
            remotePath,
            publicUrl = "https://assets.example.com",
            allowSelfSigned = false,
        };
}
