using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Database.Entities.Assets;
using Turbo.Gamedata.Assets;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players.Permissions;
using Turbo.Tests.Assets;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The panel's Assets page (<c>/api/assets</c>): staff who may see the gamedata list, open and
/// download bundles and read the checks, in the words the spec names; only those who may change it
/// upload, delete and sync. A Habbo check from the panel goes on to a sync.
/// </summary>
public sealed class AssetEndpointsTests : IAsyncDisposable
{
    private readonly Fakes _fakes = new();
    private readonly SqliteDb _db = new();
    private readonly AssetFolder _folder;
    private readonly HttpClient _client = new();
    private readonly int _port;
    private readonly IHostedService _server;

    /// <summary>The nodes the staff member holds.</summary>
    private Func<string, bool> _holds = _ => true;

    public AssetEndpointsTests()
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
                    PlayerId = 1,
                    ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                }
            );
        _fakes.Handlers["HasAsync"] = call => Task.FromResult(_holds((string)call.Args[0]!));

        _folder = new AssetFolder(_db);
        _fakes.Instances[(typeof(IAssetBundleService), null)] = new AssetBundleService(
            _db,
            Options.Create(_folder.Config),
            _folder.Store,
            new AssetBundleChecks(_db, Options.Create(_folder.Config), _folder.Store),
            TimeProvider.System,
            NullLogger<IAssetBundleService>.Instance
        );
        _db.Insert(
            new AssetBundleEntity
            {
                Id = 1,
                Kind = AssetBundleKind.Furniture,
                Name = "broken",
                Revision = "3",
                Source = AssetBundleSource.Habbo,
                Error = "Habbo has no file at its address.",
                UpdatedAt = DateTime.UtcNow,
            }
        );

        _db.Insert(
            new AssetPublishTargetEntity
            {
                Id = 1,
                Name = "Live",
                Protocol = AssetPublishProtocol.Folder,
                RemotePath = "/srv/assets",
            }
        );

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
        _folder.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task A_bundle_uploaded_is_listed_opened_downloaded_checked_and_deleted()
    {
        await _server.StartAsync(Ct);

        var uploaded = await SendAsync(
            HttpMethod.Post,
            "/api/assets/bundles",
            Upload("lamp.swf", "furniture")
        );

        uploaded.StatusCode.Should().Be(HttpStatusCode.OK);
        var bundle = await JsonAsync(uploaded);
        bundle.GetProperty("kind").GetString().Should().Be("furniture");
        bundle.GetProperty("name").GetString().Should().Be("lamp");
        bundle.GetProperty("source").GetString().Should().Be("upload");
        bundle.GetProperty("revision").ValueKind.Should().Be(JsonValueKind.Null);
        bundle.GetProperty("ids").GetArrayLength().Should().Be(0);
        bundle.GetProperty("used").GetBoolean().Should().BeFalse();
        var hash = bundle.GetProperty("hash").GetString();

        var unused = await JsonAsync(
            await SendAsync(HttpMethod.Get, "/api/assets/bundles?kind=furniture&status=unused")
        );
        unused.GetProperty("total").GetInt32().Should().Be(2);
        unused.GetProperty("pageSize").GetInt32().Should().Be(60);
        unused
            .GetProperty("items")
            .EnumerateArray()
            .Select(x => x.GetProperty("name").GetString())
            .Should()
            .Equal("broken", "lamp");
        var failed = await JsonAsync(
            await SendAsync(HttpMethod.Get, "/api/assets/bundles?status=failed")
        );
        failed.GetProperty("items")[0].GetProperty("error").GetString().Should().Contain("no file");

        var detail = await JsonAsync(
            await SendAsync(HttpMethod.Get, "/api/assets/bundles/furniture/lamp")
        );
        detail.GetProperty("path").GetString().Should().Be("bundled/furniture/lamp.nitro");
        detail.GetProperty("hash").GetString().Should().Be(hash);
        detail
            .GetProperty("files")
            .EnumerateArray()
            .Select(x => x.GetProperty("name").GetString())
            .Should()
            .Contain("test_box.json");

        var file = await SendAsync(HttpMethod.Get, "/api/assets/bundles/furniture/lamp/file");
        file.StatusCode.Should().Be(HttpStatusCode.OK);
        AssetBundleStore.HashOf(await file.Content.ReadAsByteArrayAsync(Ct)).Should().Be(hash);

        var checks = (await JsonAsync(await SendAsync(HttpMethod.Get, "/api/assets/checks")))
            .GetProperty("items")
            .EnumerateArray()
            .ToDictionary(x => x.GetProperty("id").GetString()!);
        checks["failed"].GetProperty("severity").GetString().Should().Be("warning");
        checks["failed"].GetProperty("status").GetString().Should().Be("failed");
        checks["failed"].GetProperty("kind").ValueKind.Should().Be(JsonValueKind.Null);
        checks["furniture-unused"].GetProperty("kind").GetString().Should().Be("furniture");
        checks["furniture-unused"].GetProperty("count").GetInt32().Should().Be(2);
        checks["furniture-missing"].GetProperty("severity").GetString().Should().Be("error");

        var overview = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/assets"));
        overview.GetProperty("canManage").GetBoolean().Should().BeTrue();
        overview.GetProperty("targets").GetInt32().Should().Be(1);
        overview.GetProperty("job").ValueKind.Should().Be(JsonValueKind.Null);
        overview.GetProperty("checks").GetProperty("warnings").GetInt32().Should().Be(2);
        overview
            .GetProperty("kinds")
            .EnumerateArray()
            .Select(x => x.GetProperty("kind").GetString())
            .Should()
            .Equal("furniture", "effect", "pet", "figure");

        (await SendAsync(HttpMethod.Delete, "/api/assets/bundles/furniture/lamp"))
            .StatusCode.Should()
            .Be(HttpStatusCode.NoContent);
        (await SendAsync(HttpMethod.Get, "/api/assets/bundles/furniture/lamp"))
            .StatusCode.Should()
            .Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Get, "/api/assets/bundles/chairs/lamp"))
            .StatusCode.Should()
            .Be(HttpStatusCode.BadRequest);
        (await SendAsync(HttpMethod.Get, "/api/assets/bundles?status=pretty"))
            .StatusCode.Should()
            .Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_upload_it_can_not_take_says_why()
    {
        await _server.StartAsync(Ct);

        var response = await SendAsync(
            HttpMethod.Post,
            "/api/assets/bundles",
            Upload("lamp.txt", "furniture")
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Ct)).Should().Contain(".swf, .hab or .nitro");
        (await SendAsync(HttpMethod.Post, "/api/assets/bundles", Upload("lamp.swf", "chairs")))
            .StatusCode.Should()
            .Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Only_staff_who_may_change_the_gamedata_upload_delete_and_sync()
    {
        await _server.StartAsync(Ct);
        _holds = node => node != PermissionNodes.Gamedata.MANAGE;

        (await SendAsync(HttpMethod.Get, "/api/assets/bundles"))
            .StatusCode.Should()
            .Be(HttpStatusCode.OK);
        (await JsonAsync(await SendAsync(HttpMethod.Get, "/api/assets")))
            .GetProperty("canManage")
            .GetBoolean()
            .Should()
            .BeFalse();
        (await SendAsync(HttpMethod.Post, "/api/assets/bundles", Upload("lamp.swf", "furniture")))
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Delete, "/api/assets/bundles/furniture/broken"))
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Post, "/api/assets/sync"))
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
        _fakes.Log.Of("Start").Should().BeEmpty();

        _holds = node => node == PermissionNodes.Admin.PANEL;

        (await SendAsync(HttpMethod.Get, "/api/assets/checks"))
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_sync_answers_its_job_and_a_second_while_one_runs_is_refused()
    {
        await _server.StartAsync(Ct);
        _fakes.Handlers["Start"] = _ => Job();

        var started = await JsonAsync(await SendAsync(HttpMethod.Post, "/api/assets/sync"));

        started.GetProperty("kind").GetString().Should().Be("sync");
        started.GetProperty("status").GetString().Should().Be("running");
        started.GetProperty("log").GetArrayLength().Should().Be(0);

        _fakes.Handlers["Start"] = _ => throw new InvalidOperationException("A sync is running.");

        var refused = await SendAsync(HttpMethod.Post, "/api/assets/sync");

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await SendAsync(HttpMethod.Post, "/api/assets/job/cancel"))
            .StatusCode.Should()
            .Be(HttpStatusCode.Conflict);
        (await SendAsync(HttpMethod.Get, "/api/assets/job"))
            .StatusCode.Should()
            .Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task A_Habbo_check_from_the_panel_goes_on_to_a_sync_only_when_it_answered()
    {
        await _server.StartAsync(Ct);

        (await SendAsync(HttpMethod.Post, "/api/gamedata/habbo/check"))
            .StatusCode.Should()
            .Be(HttpStatusCode.OK);
        _fakes.Log.Of("StartAfterCheck").Should().ContainSingle();

        _fakes.Handlers["CheckAsync"] = _ =>
            Task.FromException<HabboCheckResult>(new HttpRequestException("Habbo is down."));

        (await SendAsync(HttpMethod.Post, "/api/gamedata/habbo/check"))
            .StatusCode.Should()
            .Be(HttpStatusCode.BadGateway);
        _fakes.Log.Of("StartAfterCheck").Should().ContainSingle();
    }

    private static AssetJobSnapshot Job() =>
        new()
        {
            Id = Guid.NewGuid(),
            Kind = AssetJobKind.Sync,
            Title = "Sync from habbo.com",
            Status = AssetJobStatus.Running,
            Phase = "Starting",
            Total = 0,
            Done = 0,
            Failed = 0,
            Log = [],
            PlayerId = 1,
            StartedAt = DateTime.UtcNow,
        };

    private static MultipartFormDataContent Upload(string fileName, string kind)
    {
        var file = new ByteArrayContent(NitroConverterTests.Swf());

        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        return new MultipartFormDataContent
        {
            { file, "file", fileName },
            { new StringContent(kind), "kind" },
        };
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        HttpContent? content = null
    )
    {
        var request = new HttpRequestMessage(method, $"http://127.0.0.1:{_port}{path}")
        {
            Content = content,
        };

        request.Headers.Authorization = new("Bearer", "a-session");

        return await _client.SendAsync(request, Ct);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;
}
