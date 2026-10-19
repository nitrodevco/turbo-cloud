using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Players.Permissions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The panel's catalogue import (<c>/api/catalog/import</c>, and <c>/import/logs</c> for packet
/// logs sent as a form): seeing what a capture would do
/// takes <c>admin.catalog.import</c>, making it <c>catalog.manage</c> as well; staff without them
/// are refused before the capture is read.
/// </summary>
public sealed class CatalogImportEndpointsTests : IAsyncDisposable
{
    private readonly Fakes _fakes = new();
    private readonly HttpClient _client = new();
    private readonly int _port;
    private readonly IHostedService _server;

    /// <summary>The nodes the staff member holds.</summary>
    private Func<string, bool> _holds = _ => true;

    public CatalogImportEndpointsTests()
    {
        _port = TestPorts.Free();

        _fakes.Handlers["GetSessionAsync"] = _ =>
            Task.FromResult<AdminSessionSnapshot?>(
                new AdminSessionSnapshot
                {
                    PlayerId = 1,
                    ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                }
            );
        _fakes.Handlers["HasAsync"] = call => Task.FromResult(_holds((string)call.Args[0]!));

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
    }

    [Fact]
    public async Task Without_the_import_node_neither_the_plan_nor_the_import_is_made()
    {
        _holds = node => node != PermissionNodes.Admin.CATALOG_IMPORT;
        await _server.StartAsync(Ct);

        (await SendAsync("/api/catalog/import/preview"))
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
        (await SendAsync("/api/catalog/import")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task With_the_import_node_but_not_manage_the_plan_is_read_but_not_made()
    {
        _holds = node => node != PermissionNodes.Catalog.MANAGE;
        await _server.StartAsync(Ct);

        // Let through, a capture with no index is refused for what it is.
        var plan = await SendAsync("/api/catalog/import/preview");
        plan.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        JsonDocument
            .Parse(await plan.Content.ReadAsStringAsync(Ct))
            .RootElement.GetProperty("message")
            .GetString()
            .Should()
            .Contain("no Builders Club index");

        (await SendAsync("/api/catalog/import")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_packet_logs_take_the_same_nodes_and_their_files_are_read()
    {
        _holds = node => node != PermissionNodes.Catalog.MANAGE;
        await _server.StartAsync(Ct);

        // Let through, a normal catalogue log with no index in it is refused for what it is.
        var plan = await SendLogsAsync("/api/catalog/import/logs/preview");
        plan.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        JsonDocument
            .Parse(await plan.Content.ReadAsStringAsync(Ct))
            .RootElement.GetProperty("message")
            .GetString()
            .Should()
            .Contain("normal catalogue's log has no index of it");

        (await SendLogsAsync("/api/catalog/import/logs"))
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);

        _holds = node => node != PermissionNodes.Admin.CATALOG_IMPORT;

        (await SendLogsAsync("/api/catalog/import/logs/preview"))
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
    }

    private async Task<HttpResponseMessage> SendLogsAsync(string path)
    {
        var form = new MultipartFormDataContent
        {
            {
                new ByteArrayContent("[Chat]\nIncoming[1] -> [0]"u8.ToArray()),
                "normal",
                "normal.txt"
            },
            { new StringContent("false"), "takeOthersOut" },
        };
        var request = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{_port}{path}")
        {
            Content = form,
        };

        request.Headers.Authorization = new("Bearer", "a-session");

        return await _client.SendAsync(request, Ct);
    }

    private async Task<HttpResponseMessage> SendAsync(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{_port}{path}")
        {
            Content = JsonContent.Create(new { capture = new { }, takeOthersOut = false }),
        };

        request.Headers.Authorization = new("Bearer", "a-session");

        return await _client.SendAsync(request, Ct);
    }
}
