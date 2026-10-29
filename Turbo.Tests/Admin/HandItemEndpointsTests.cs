using System.Net;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players.Permissions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

public sealed class HandItemEndpointsTests : IAsyncDisposable
{
    private readonly Fakes _fakes = new();
    private readonly HttpClient _client = new();
    private readonly IHostedService _server;
    private readonly int _port = TestPorts.Free();
    private bool _canView = true;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public HandItemEndpointsTests()
    {
        _fakes.Handlers["GetSessionAsync"] = _ =>
            Task.FromResult<AdminSessionSnapshot?>(
                new AdminSessionSnapshot
                {
                    PlayerId = 1,
                    ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                }
            );
        _fakes.Handlers["HasAsync"] = call =>
            Task.FromResult(
                (string)call.Args[0]! != PermissionNodes.Admin.GAMEDATA_VIEW || _canView
            );
        _fakes.Handlers["GetAsync"] = call =>
            call.Interface == typeof(IHandItemCatalog)
                ? Task.FromResult(
                    new HandItemCatalogSnapshot
                    {
                        Revision = "fixture-revision",
                        Items =
                        [
                            new HandItemSnapshot
                            {
                                Id = 1128,
                                Name = "Bow",
                                AssetPartId = 311,
                                Library = "hh_human_item",
                                AssetAvailable = true,
                                PreviewAvailable = true,
                            },
                        ],
                    }
                )
                : Fakes.NotHandled;
        _fakes.Handlers["GetPreviewAsync"] = call =>
            Task.FromResult<byte[]?>((int)call.Args[0]! == 1128 ? [137, 80, 78, 71] : null);
        (_server, _, _) = AdminApiServerTests.Build(
            $"http://127.0.0.1:{_port}",
            _fakes,
            NullLoggerFactory.Instance
        );
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _server.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Viewing_staff_can_browse_the_catalogue_and_download_a_local_preview()
    {
        await _server.StartAsync(Ct);
        using var response = await GetAsync("/api/gamedata/hand-items");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var catalog = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;
        catalog.GetProperty("revision").GetString().Should().Be("fixture-revision");
        catalog.GetProperty("items")[0].GetProperty("id").GetInt32().Should().Be(1128);
        catalog.GetProperty("items")[0].GetProperty("assetPartId").GetInt32().Should().Be(311);
        using var preview = await GetAsync("/api/gamedata/hand-items/1128/preview");
        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        preview.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        (await preview.Content.ReadAsByteArrayAsync(Ct)).Should().Equal([137, 80, 78, 71]);
        using var missing = await GetAsync("/api/gamedata/hand-items/999/preview");
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Catalogue_and_previews_require_the_same_view_permission()
    {
        await _server.StartAsync(Ct);
        _canView = false;
        using var catalog = await GetAsync("/api/gamedata/hand-items");
        using var preview = await GetAsync("/api/gamedata/hand-items/1128/preview");
        catalog.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        preview.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<HttpResponseMessage> GetAsync(string path)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"http://127.0.0.1:{_port}{path}"
        );
        request.Headers.Authorization = new("Bearer", "fixture-session");
        return await _client.SendAsync(request, Ct);
    }
}
