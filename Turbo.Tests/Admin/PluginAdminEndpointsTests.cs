using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Admin.Configuration;
using Turbo.Contracts.Plugins.Admin;
using Turbo.Plugins;
using Turbo.Plugins.Configuration;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Plugins;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// What a plugin adds to the panel, over the real admin API, the real plugin manager and a real
/// plugin assembly: its pages go to staff holding their node; its endpoints under
/// <c>/api/plugins/&lt;key&gt;/api/</c> are guarded by its own nodes (registered while it is
/// loaded, granted to no one), capped in body size and time, audited when they change something,
/// and an exception in one is an error answer that leaves the host serving. Off, gone, reloading
/// and unknown plugins each say so; a plugin that maps a route twice or guards one with a node
/// not its own is refused.
/// </summary>
public sealed class PluginAdminEndpointsTests : IAsyncDisposable
{
    private const string VIEW = "plugin.sample.items.view";
    private const string MANAGE = "plugin.sample.items.manage";

    private readonly Fakes _fakes = new();
    private readonly SqliteDb _db = new();
    private readonly HttpClient _client = new();
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-plugin-admin-" + Guid.NewGuid().ToString("N")
    );
    private readonly int _port = TestPorts.Free();
    private readonly PluginAdminRoutes _routes = new();
    private readonly Registry _permissions = new();
    private readonly PluginManager _manager;
    private readonly IHostedService _server;

    /// <summary>The nodes the staff member holds.</summary>
    private Func<string, bool> _holds = _ => true;

    public PluginAdminEndpointsTests()
    {
        Directory.CreateDirectory(_folder);

        _fakes.Handlers["GetSessionAsync"] = _ =>
            Task.FromResult<AdminSessionSnapshot?>(
                new AdminSessionSnapshot
                {
                    PlayerId = 1,
                    ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                }
            );
        _fakes.Handlers["HasAsync"] = call => Task.FromResult(_holds((string)call.Args[0]!));
        _fakes.Handlers["CreateDbContextAsync"] = _ => _db.CreateDbContextAsync();

        _manager = new PluginManager(
            new ServiceCollection()
                .AddSingleton(_routes)
                .AddSingleton<IPermissionRegistryProvider>(_permissions)
                .BuildServiceProvider(),
            new AssemblyProcessor([]),
            Options.Create(new PluginConfig { PluginFolderPath = _folder }),
            NullLogger<PluginManager>.Instance
        );

        (_server, _, _) = AdminApiServerTests.Build(
            $"http://127.0.0.1:{_port}",
            _fakes,
            NullLoggerFactory.Instance,
            new AdminConfig
            {
                Enabled = true,
                Url = $"http://127.0.0.1:{_port}",
                PluginMaxBodyBytes = 64,
                PluginTimeoutSeconds = 1,
            },
            _routes
        );
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _server.StopAsync(CancellationToken.None);
        await _manager.UnloadAllAsync(CancellationToken.None);
        _db.Dispose();

        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task A_plugins_pages_and_endpoints_need_its_own_nodes_which_no_one_holds_by_default()
    {
        await LoadAsync();

        _permissions.Current.Nodes[VIEW].GrantedByDefault.Should().BeFalse();
        _permissions.Current.Nodes[MANAGE].GrantedByDefault.Should().BeFalse();

        _holds = node => node == PermissionNodes.Admin.PANEL;

        (await JsonAsync(await SendAsync(HttpMethod.Get, "/api/plugins/pages")))
            .GetArrayLength()
            .Should()
            .Be(0);
        (await SendAsync(HttpMethod.Get, "/api/plugins/sample/api/items"))
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);

        _holds = node => node is PermissionNodes.Admin.PANEL or VIEW;

        var plugin = (await JsonAsync(await SendAsync(HttpMethod.Get, "/api/plugins/pages")))
            .EnumerateArray()
            .Single();

        plugin.GetProperty("key").GetString().Should().Be("sample");
        plugin.GetProperty("revision").GetString().Should().NotBeNullOrEmpty();
        plugin
            .GetProperty("nodes")
            .EnumerateArray()
            .Select(x => x.GetString())
            .Should()
            .Equal(VIEW);
        var page = plugin.GetProperty("pages").EnumerateArray().Single();
        page.GetProperty("id").GetString().Should().Be("items");
        page.GetProperty("blocks")[0].GetProperty("type").GetString().Should().Be("table");

        var items = await SendAsync(HttpMethod.Get, "/api/plugins/sample/api/items?page=2");

        items.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonAsync(items)).GetProperty("page").GetString().Should().Be("2");

        // Viewing is not changing.
        (
            await SendJsonAsync(
                HttpMethod.Post,
                "/api/plugins/sample/api/items/7/echo",
                new { a = 1 }
            )
        )
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
        Log().Should().Equal(("call sample POST /items/7/echo 403", "refused"));
    }

    [Fact]
    public async Task A_change_is_audited_and_an_oversized_or_broken_body_never_reaches_the_plugin()
    {
        await LoadAsync();

        var echo = await SendJsonAsync(
            HttpMethod.Post,
            "/api/plugins/sample/api/items/7/echo",
            new { a = 1 }
        );

        echo.StatusCode.Should().Be(HttpStatusCode.OK);
        var answer = await JsonAsync(echo);
        answer.GetProperty("id").GetString().Should().Be("7");
        answer.GetProperty("body").GetProperty("a").GetInt32().Should().Be(1);
        answer.GetProperty("player").GetInt32().Should().Be(1);

        (
            await SendJsonAsync(
                HttpMethod.Post,
                "/api/plugins/sample/api/items/7/echo",
                new { text = new string('x', 100) }
            )
        )
            .StatusCode.Should()
            .Be(HttpStatusCode.RequestEntityTooLarge);

        var broken = new HttpRequestMessage(
            HttpMethod.Post,
            $"http://127.0.0.1:{_port}/api/plugins/sample/api/items/7/echo"
        )
        {
            Content = new StringContent("{not json", Encoding.UTF8, "application/json"),
        };
        broken.Headers.Authorization = new("Bearer", "a-session");
        (await _client.SendAsync(broken, Ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        Log()
            .Should()
            .Equal(
                ("call sample POST /items/7/echo 200", "completed"),
                ("call sample POST /items/7/echo 413", "failed"),
                ("call sample POST /items/7/echo 400", "failed")
            );
    }

    [Fact]
    public async Task A_plugin_that_throws_or_hangs_gets_an_error_answer_and_the_host_keeps_serving()
    {
        await LoadAsync();

        var boom = await SendAsync(HttpMethod.Delete, "/api/plugins/sample/api/items/7");

        boom.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (await JsonAsync(boom)).GetProperty("message").GetString().Should().Contain("kaboom");

        (await SendAsync(HttpMethod.Get, "/api/plugins/sample/api/slow"))
            .StatusCode.Should()
            .Be(HttpStatusCode.GatewayTimeout);

        (await SendAsync(HttpMethod.Get, "/api/plugins/sample/api/items"))
            .StatusCode.Should()
            .Be(HttpStatusCode.OK);
        Log().Should().Equal(("call sample DELETE /items/7 500", "error"));
    }

    [Fact]
    public async Task An_unknown_reloading_off_or_removed_plugin_says_so_and_its_nodes_go_with_it()
    {
        await LoadAsync();

        (await SendAsync(HttpMethod.Get, "/api/plugins/nothing/api/items"))
            .StatusCode.Should()
            .Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Get, "/api/plugins/sample/api/nowhere"))
            .StatusCode.Should()
            .Be(HttpStatusCode.NotFound);

        _routes.Reloading("sample");
        (await SendAsync(HttpMethod.Get, "/api/plugins/sample/api/items"))
            .StatusCode.Should()
            .Be(HttpStatusCode.ServiceUnavailable);

        await _manager.ReloadAsync("sample", Ct);
        (await SendAsync(HttpMethod.Get, "/api/plugins/sample/api/items"))
            .StatusCode.Should()
            .Be(HttpStatusCode.OK);

        Directory.Move(Path.Combine(_folder, "sample"), Path.Combine(_folder, ".away"));
        await _manager.ReloadAsync("sample", Ct);

        var gone = await SendAsync(HttpMethod.Get, "/api/plugins/sample/api/items");

        gone.StatusCode.Should().Be(HttpStatusCode.Gone);
        (await JsonAsync(gone))
            .GetProperty("message")
            .GetString()
            .Should()
            .Contain("Reload the page");
        (await JsonAsync(await SendAsync(HttpMethod.Get, "/api/plugins/pages")))
            .GetArrayLength()
            .Should()
            .Be(0);
        _permissions.Current.IsRegistered(VIEW).Should().BeFalse();
    }

    [Theory]
    [InlineData("twice", "maps GET items twice")]
    [InlineData("borrowed", "which is not one of its own nodes")]
    public async Task A_plugin_mapping_a_route_twice_or_guarding_one_with_a_borrowed_node_is_refused(
        string flaw,
        string error
    )
    {
        PluginBuilder.Write(_folder, "sample", Source(flaw));
        await _manager.LoadAllAsync(ct: Ct);

        var status = _manager.Snapshot().Single(x => x.Key == "sample");

        status.State.Should().Be(PluginStateType.Failed);
        status.Error.Should().Contain(error);
        _permissions.Current.IsRegistered(VIEW).Should().BeFalse();
        await _server.StartAsync(Ct);
        // Never live, so nothing of it is served.
        (await SendAsync(HttpMethod.Get, "/api/plugins/sample/api/items"))
            .StatusCode.Should()
            .Be(HttpStatusCode.NotFound);
    }

    private async Task LoadAsync()
    {
        PluginBuilder.Write(_folder, "sample", Source(null));
        await _manager.LoadAllAsync(ct: Ct);
        _manager
            .Snapshot()
            .Single()
            .State.Should()
            .Be(PluginStateType.Loaded, _manager.Snapshot().Single().Error);
        await _server.StartAsync(Ct);
    }

    private static string Source(string? flaw) =>
        $$"""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using Turbo.Contracts.Plugins;
            using Turbo.Contracts.Plugins.Admin;
            using Turbo.Contracts.Plugins.Exports;

            public sealed class SampleAdmin : IPluginAdmin
            {
                public void Configure(IPluginAdminBuilder admin)
                {
                    var view = admin.Node("items.view", "See the items");
                    var manage = admin.Node("items.manage", "Change the items");

                    admin.Map("GET", "items", view, (r, ct) =>
                        Task.FromResult(PluginAdminResult.Ok(new { page = r.Query.TryGetValue("page", out var p) ? p : "0" })));
                    admin.Map("POST", "items/{id}/echo", manage, (r, ct) =>
                        Task.FromResult(PluginAdminResult.Ok(new { id = r.Route["id"], body = r.Body, player = r.PlayerId })));
                    admin.Map("DELETE", "items/{id}", manage, (r, ct) => throw new InvalidOperationException("kaboom"));
                    admin.Map("GET", "slow", view, (r, ct) => Task.Delay(Timeout.Infinite, CancellationToken.None)
                        .ContinueWith(_ => PluginAdminResult.NoContent()));
                    {{flaw switch
            {
                "twice" => "admin.Map(\"GET\", \"items\", view, (r, ct) => Task.FromResult(PluginAdminResult.NoContent()));",
                "borrowed" => "admin.Map(\"GET\", \"other\", \"admin.plugins.manage\", (r, ct) => Task.FromResult(PluginAdminResult.NoContent()));",
                _ => "",
            }}}
                    admin.Page(new PluginAdminPage("items", "Items", view,
                        [new PluginAdminBlock("table", Endpoint: "items", Columns: [new PluginAdminColumn("id", "Id")])]));
                }
            }

            public sealed class SamplePlugin : ITurboPlugin
            {
                public string Key => "sample";
                public string Version => "1.0.0";
                public void ConfigureServices(IServiceCollection services, PluginManifest manifest) =>
                    services.AddSingleton<IPluginAdmin, SampleAdmin>();
                public Task StartAsync(IServiceProvider services, CancellationToken ct) => Task.CompletedTask;
                public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
                public Task BindExportsAsync(IExportBinder binder, IServiceProvider services) => Task.CompletedTask;
                public ValueTask DisposeAsync() => ValueTask.CompletedTask;
            }
            """;

    private (string Arguments, string Outcome)[] Log()
    {
        using var db = _db.CreateDbContext();

        return
        [
            .. db.CommandLogs.OrderBy(x => x.Id).ToList().Select(x => (x.Arguments, x.Outcome)),
        ];
    }

    private async Task<HttpResponseMessage> SendJsonAsync(
        HttpMethod method,
        string path,
        object body
    )
    {
        var request = new HttpRequestMessage(method, $"http://127.0.0.1:{_port}{path}")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json"
            ),
        };

        request.Headers.Authorization = new("Bearer", "a-session");

        return await _client.SendAsync(request, Ct);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, $"http://127.0.0.1:{_port}{path}");

        request.Headers.Authorization = new("Bearer", "a-session");

        return await _client.SendAsync(request, Ct);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;

    /// <summary>The permission registry as the host keeps it: core's nodes, and sources that come and go.</summary>
    private sealed class Registry : IPermissionRegistryProvider
    {
        private readonly List<IPermissionNodeSource> _sources = [new CorePermissionNodeSource()];

        public PermissionRegistry Current { get; private set; } =
            new([new CorePermissionNodeSource()]);

        public event Action? Changed;

        public IDisposable Register(IPermissionNodeSource source)
        {
            Current = new PermissionRegistry([.. _sources, source]);
            _sources.Add(source);
            Changed?.Invoke();

            return new Removal(() =>
            {
                _sources.Remove(source);
                Current = new PermissionRegistry(_sources);
            });
        }

        private sealed class Removal(Action remove) : IDisposable
        {
            public void Dispose() => remove();
        }
    }
}
