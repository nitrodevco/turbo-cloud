using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
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

public sealed class VendingRepairEndpointsTests : IAsyncDisposable
{
    private readonly Fakes _fakes = new();
    private readonly HttpClient _client = new();
    private readonly IHostedService _server;
    private readonly int _port = TestPorts.Free();
    private bool _canView = true;
    private bool _canManage = true;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public VendingRepairEndpointsTests()
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
                (string)call.Args[0]! switch
                {
                    PermissionNodes.Admin.GAMEDATA_VIEW => _canView,
                    PermissionNodes.Gamedata.MANAGE => _canManage,
                    _ => true,
                }
            );
        _fakes.Handlers["PreviewVendingRepairAsync"] = _ =>
            Task.FromResult(
                new VendingRepairPreviewSnapshot
                {
                    Items =
                    [
                        new VendingRepairItemSnapshot
                        {
                            Id = 42,
                            ClassName = "fridge",
                            Version = "expected-version",
                            Before = "{}",
                            After = "{\"logic\":\"vending_machine\"}",
                            HandItems = [3, 4, 5, 6],
                            Animates = true,
                            CurrentUsagePolicy = 0,
                            DefaultUsagePolicy = 2,
                        },
                    ],
                    Total = 1,
                    Page = 0,
                    PageSize = 60,
                }
            );
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
    public async Task Preview_is_readable_without_manage_but_applying_requires_manage()
    {
        await _server.StartAsync(Ct);
        _canManage = false;
        using var preview = await SendAsync(HttpMethod.Get);
        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await preview.Content.ReadAsStringAsync(Ct));
        json.RootElement.GetProperty("items")[0]
            .GetProperty("version")
            .GetString()
            .Should()
            .Be("expected-version");
        using var denied = await SendAsync(HttpMethod.Post);
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _fakes
            .Log.On<IGamedataFurnitureService>()
            .Should()
            .NotContain(x => x.Method == "RepairVendingAsync");
    }

    [Fact]
    public async Task Both_routes_require_view_permission()
    {
        await _server.StartAsync(Ct);
        _canView = false;
        using var preview = await SendAsync(HttpMethod.Get);
        using var repair = await SendAsync(HttpMethod.Post);
        preview.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        repair.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(false, HttpStatusCode.BadRequest)]
    [InlineData(true, HttpStatusCode.Conflict)]
    public async Task Invalid_and_stale_selections_have_distinct_responses(
        bool stale,
        HttpStatusCode expected
    )
    {
        await _server.StartAsync(Ct);
        _fakes.Handlers["RepairVendingAsync"] = _ =>
            stale
                ? throw new VendingRepairConflictException("Refresh this preview.")
                : throw new ArgumentException("Invalid selection.");
        using var response = await SendAsync(HttpMethod.Post);
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task Apply_passes_expected_versions_and_explicit_access_selection()
    {
        await _server.StartAsync(Ct);
        using var response = await SendAsync(HttpMethod.Post);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var call = _fakes
            .Log.On<IGamedataFurnitureService>()
            .Single(x => x.Method == "RepairVendingAsync");
        var item = ((IReadOnlyCollection<VendingRepairSelectionSnapshot>)call.Args[0]!).Single();
        item.Id.Should().Be(42);
        item.Version.Should().Be("expected-version");
        item.UseDefaultUsagePolicy.Should().BeTrue();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method)
    {
        using var request = new HttpRequestMessage(
            method,
            $"http://127.0.0.1:{_port}/api/gamedata/furniture/vending-repair"
        );
        request.Headers.Authorization = new("Bearer", "fixture-session");
        if (method == HttpMethod.Post)
            request.Content = JsonContent.Create(
                new
                {
                    items = new[]
                    {
                        new
                        {
                            id = 42,
                            version = "expected-version",
                            useDefaultUsagePolicy = true,
                        },
                    },
                }
            );
        return await _client.SendAsync(request, Ct);
    }
}
