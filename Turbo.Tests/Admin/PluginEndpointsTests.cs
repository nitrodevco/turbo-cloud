using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Plugins;
using Turbo.Plugins.Configuration;
using Turbo.Plugins.Sources;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Plugins;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The panel's Plugins page (<c>/api/plugins</c>) over the real plugin manager and a plugin
/// folder on disk: staff with <c>admin.plugins.view</c> list the plugins; reloading, rescanning
/// and turning one off or on need <c>admin.plugins.manage</c>, and turning one off lasts across a
/// restart; installing a package needs <c>admin.plugins.install</c> and the upload switch, and a
/// package whose manifest is wrong changes nothing on disk. Every action, refused ones too, is in
/// the command log.
/// </summary>
public sealed class PluginEndpointsTests : IAsyncDisposable
{
    private readonly Fakes _fakes = new();
    private readonly SqliteDb _db = new();
    private readonly HttpClient _client = new();
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-plugins-" + Guid.NewGuid().ToString("N")
    );
    private readonly int _port;
    private readonly IHostedService _server;

    private PluginAdministration _plugins;
    private PluginSources _sources = null!;
    private bool _uploadEnabled = true;

    /// <summary>The nodes the staff member holds.</summary>
    private Func<string, bool> _holds = _ => true;

    public PluginEndpointsTests()
    {
        Directory.CreateDirectory(_folder);

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
        _fakes.Handlers["CreateDbContextAsync"] = _ => _db.CreateDbContextAsync();

        _plugins = Restart();

        // The endpoints reach the plugins through the interface the host registers.
        _fakes.Handlers["get_UploadEnabled"] = _ => _plugins.UploadEnabled;
        _fakes.Handlers["get_MaxUploadBytes"] = _ => _plugins.MaxUploadBytes;
        _fakes.Handlers["List"] = call =>
            call.Interface == typeof(IPluginSources) ? _sources.List() : _plugins.List();
        _fakes.Handlers["VersionsOf"] = call => _plugins.VersionsOf((string)call.Args[0]!);
        _fakes.Handlers["RollbackAsync"] = call =>
            _plugins.RollbackAsync((string)call.Args[0]!, (string)call.Args[1]!, Ct);
        _fakes.Handlers["get_AllowedOwners"] = _ => _sources.AllowedOwners;
        _fakes.Handlers["get_CheckMinutes"] = _ => _sources.CheckMinutes;
        _fakes.Handlers["get_Job"] = _ => _sources.Job;
        _fakes.Handlers["Add"] = call =>
            _sources.Add((PluginSourceInput)call.Args[0]!, (int)call.Args[1]!);
        _fakes.Handlers["Edit"] = call =>
            _sources.Edit((string)call.Args[0]!, (PluginSourceInput)call.Args[1]!);
        _fakes.Handlers["RescanAsync"] = _ => _plugins.RescanAsync(Ct);
        _fakes.Handlers["ReloadAsync"] = call => _plugins.ReloadAsync((string)call.Args[0]!, Ct);
        _fakes.Handlers["SetEnabledAsync"] = call =>
            _plugins.SetEnabledAsync((string)call.Args[0]!, (bool)call.Args[1]!, Ct);
        _fakes.Handlers["InstallAsync"] = call => _plugins.InstallAsync((Stream)call.Args[0]!, Ct);

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

        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task Staff_without_the_view_node_cannot_list_plugins()
    {
        _holds = node => node == PermissionNodes.Admin.PANEL;
        await _server.StartAsync(Ct);

        (await SendAsync(HttpMethod.Get, "/api/plugins"))
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_plugin_on_disk_is_listed_with_its_manifest_and_why_it_did_not_load()
    {
        WritePlugin("sample", "Sample Plugin");
        await _plugins.RescanAsync(Ct);
        await _server.StartAsync(Ct);

        var list = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/plugins"));
        var plugin = list.GetProperty("plugins").EnumerateArray().Single();

        plugin.GetProperty("key").GetString().Should().Be("sample");
        plugin.GetProperty("name").GetString().Should().Be("Sample Plugin");
        plugin.GetProperty("version").GetString().Should().Be("1.2.3");
        plugin.GetProperty("author").GetString().Should().Be("Staff");
        plugin.GetProperty("folder").GetString().Should().Be("sample");
        plugin.GetProperty("state").GetString().Should().Be("failed");
        plugin.GetProperty("error").GetString().Should().NotBeNullOrEmpty();
        list.GetProperty("canManage").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task A_folder_whose_manifest_is_unreadable_is_listed_as_failed()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "broken"));
        await File.WriteAllTextAsync(Path.Combine(_folder, "broken", "manifest.json"), "{", Ct);
        await _plugins.RescanAsync(Ct);
        await _server.StartAsync(Ct);

        var plugin = (await JsonAsync(await SendAsync(HttpMethod.Get, "/api/plugins")))
            .GetProperty("plugins")
            .EnumerateArray()
            .Single();

        plugin.GetProperty("key").GetString().Should().Be("broken");
        plugin.GetProperty("name").ValueKind.Should().Be(JsonValueKind.Null);
        plugin.GetProperty("state").GetString().Should().Be("failed");
    }

    [Fact]
    public async Task Staff_who_may_only_view_change_nothing_and_the_refusal_is_logged()
    {
        WritePlugin("sample", "Sample Plugin");
        _holds = node => node != PermissionNodes.Admin.PLUGINS_MANAGE;
        await _server.StartAsync(Ct);

        var list = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/plugins"));
        list.GetProperty("canManage").GetBoolean().Should().BeFalse();
        list.GetProperty("canInstall").GetBoolean().Should().BeFalse();

        foreach (var path in new[] { "sample/disable", "sample/enable", "sample/reload", "rescan" })
            (await SendAsync(HttpMethod.Post, $"/api/plugins/{path}"))
                .StatusCode.Should()
                .Be(HttpStatusCode.Forbidden);

        File.Exists(Path.Combine(_folder, PluginStateFile.FILE_NAME)).Should().BeFalse();
        Log()
            .Should()
            .Equal([
                ("disable sample", "refused"),
                ("enable sample", "refused"),
                ("reload sample", "refused"),
                ("rescan", "refused"),
            ]);
    }

    [Fact]
    public async Task A_plugin_turned_off_stays_off_after_a_restart_until_turned_on()
    {
        WritePlugin("sample", "Sample Plugin");
        await _server.StartAsync(Ct);

        var off = await SendAsync(HttpMethod.Post, "/api/plugins/sample/disable");

        off.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonAsync(off))
            .GetProperty("plugin")
            .GetProperty("state")
            .GetString()
            .Should()
            .Be("disabled");

        // A restart: a new manager reads the folder and the state file, as the host does at start.
        _plugins = Restart();
        await _plugins.RescanAsync(Ct);

        var listed = (await JsonAsync(await SendAsync(HttpMethod.Get, "/api/plugins")))
            .GetProperty("plugins")
            .EnumerateArray()
            .Single();
        listed.GetProperty("state").GetString().Should().Be("disabled");
        listed.GetProperty("error").ValueKind.Should().Be(JsonValueKind.Null);

        var reload = await SendAsync(HttpMethod.Post, "/api/plugins/sample/reload");
        reload.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // Turned on, it is loaded again: this one's assembly is not a real one, so the load fails
        // and says why, but it is no longer off.
        var on = await SendAsync(HttpMethod.Post, "/api/plugins/sample/enable");

        on.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await JsonAsync(on))
            .GetProperty("plugin")
            .GetProperty("state")
            .GetString()
            .Should()
            .Be("failed");
        _plugins = Restart();
        await _plugins.RescanAsync(Ct);
        _plugins.List().Single().State.Should().Be(Turbo.Primitives.Plugins.PluginStateType.Failed);

        Log()
            .Should()
            .Equal([
                ("disable sample", "completed"),
                ("reload sample", "failed"),
                ("enable sample", "failed"),
            ]);
    }

    [Fact]
    public async Task An_unknown_plugin_is_not_found()
    {
        await _server.StartAsync(Ct);

        (await SendAsync(HttpMethod.Post, "/api/plugins/nothing/disable"))
            .StatusCode.Should()
            .Be(HttpStatusCode.NotFound);
        File.Exists(Path.Combine(_folder, PluginStateFile.FILE_NAME)).Should().BeFalse();
    }

    [Fact]
    public async Task A_package_is_installed_into_the_plugin_folder_under_its_key()
    {
        await _server.StartAsync(Ct);

        var response = await InstallAsync(
            Package(("manifest.json", Manifest("sample", "Sample Plugin")), ("Sample.dll", "MZ"))
        );

        // Its assembly is not a real one, so it is installed and its load fails, saying why.
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var plugin = (await JsonAsync(response)).GetProperty("plugin");
        plugin.GetProperty("key").GetString().Should().Be("sample");
        plugin.GetProperty("state").GetString().Should().Be("failed");
        File.Exists(Path.Combine(_folder, "sample", "manifest.json")).Should().BeTrue();
        File.Exists(Path.Combine(_folder, "sample", "Sample.dll")).Should().BeTrue();
        Log().Should().Equal([("install sample 1.2.3", "failed")]);
    }

    [Fact]
    public async Task A_zipped_plugin_folder_is_installed_from_its_top_folder()
    {
        await _server.StartAsync(Ct);

        await InstallAsync(
            Package(
                ("bundle/manifest.json", Manifest("sample", "Sample Plugin")),
                ("bundle/Sample.dll", "MZ")
            )
        );

        File.Exists(Path.Combine(_folder, "sample", "Sample.dll")).Should().BeTrue();
    }

    [Theory]
    [InlineData(
        """{ "Name": "No key", "Version": "1", "Author": "a", "AssemblyFile": "Sample.dll" }"""
    )]
    [InlineData(
        """{ "Name": "Bad", "Key": "../evil", "Version": "1", "Author": "a", "AssemblyFile": "Sample.dll" }"""
    )]
    [InlineData(
        """{ "Name": "No version", "Key": "sample", "Author": "a", "AssemblyFile": "Sample.dll" }"""
    )]
    [InlineData("not json")]
    public async Task A_package_with_a_bad_manifest_is_refused_and_changes_nothing(string manifest)
    {
        await _server.StartAsync(Ct);

        var response = await InstallAsync(
            Package(("manifest.json", manifest), ("Sample.dll", "MZ"))
        );

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await JsonAsync(response);
        body.GetProperty("ok").GetBoolean().Should().BeFalse();
        body.GetProperty("message").GetString().Should().NotContain(Path.GetTempPath());
        Directory.EnumerateFileSystemEntries(_folder).Should().BeEmpty();
        Log().Single().Outcome.Should().Be("failed");
    }

    [Fact]
    public async Task A_package_with_a_file_outside_its_folder_is_refused()
    {
        await _server.StartAsync(Ct);

        var response = await InstallAsync(
            Package(
                ("manifest.json", Manifest("sample", "Sample Plugin")),
                ("Sample.dll", "MZ"),
                ("../escaped.dll", "MZ")
            )
        );

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        Directory.EnumerateFileSystemEntries(_folder).Should().BeEmpty();
        File.Exists(Path.Combine(Path.GetTempPath(), "escaped.dll")).Should().BeFalse();
    }

    [Fact]
    public async Task A_package_without_a_manifest_or_not_a_zip_is_refused()
    {
        await _server.StartAsync(Ct);

        (await InstallAsync(Package(("Sample.dll", "MZ"))))
            .StatusCode.Should()
            .Be(HttpStatusCode.UnprocessableEntity);
        (await InstallAsync(Encoding.UTF8.GetBytes("not a zip")))
            .StatusCode.Should()
            .Be(HttpStatusCode.UnprocessableEntity);
        Directory.EnumerateFileSystemEntries(_folder).Should().BeEmpty();
    }

    [Fact]
    public async Task Installing_needs_the_install_node_by_name_besides_manage()
    {
        _holds = node => node != PermissionNodes.Admin.PLUGINS_INSTALL;
        await _server.StartAsync(Ct);

        (await JsonAsync(await SendAsync(HttpMethod.Get, "/api/plugins")))
            .GetProperty("canInstall")
            .GetBoolean()
            .Should()
            .BeFalse();
        (
            await InstallAsync(
                Package(("manifest.json", Manifest("sample", "Sample")), ("Sample.dll", "MZ"))
            )
        )
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
        Directory.EnumerateFileSystemEntries(_folder).Should().BeEmpty();
        Log().Should().Equal([("install", "refused")]);
    }

    [Fact]
    public async Task Installing_is_refused_while_the_server_does_not_allow_uploads()
    {
        _uploadEnabled = false;
        _plugins = Restart();
        await _server.StartAsync(Ct);

        var list = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/plugins"));
        list.GetProperty("uploadEnabled").GetBoolean().Should().BeFalse();

        var response = await InstallAsync(
            Package(("manifest.json", Manifest("sample", "Sample")), ("Sample.dll", "MZ"))
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await JsonAsync(response))
            .GetProperty("message")
            .GetString()
            .Should()
            .Contain("UploadEnabled");
        Directory.EnumerateFileSystemEntries(_folder).Should().BeEmpty();
    }

    [Fact]
    public async Task Linking_a_repository_needs_the_install_node_and_the_refusal_is_logged()
    {
        _holds = node => node != PermissionNodes.Admin.PLUGINS_INSTALL;
        await _server.StartAsync(Ct);

        var response = await SendJsonAsync(
            HttpMethod.Post,
            "/api/plugins/sources",
            new { repository = "Diddyy/turbo-bugreport-plugin", follow = "latest" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        Log().Should().Equal([("source add Diddyy/turbo-bugreport-plugin", "refused")]);
        _sources.List().Should().BeEmpty();
    }

    [Fact]
    public async Task A_linked_repository_is_listed_with_whether_it_has_a_token_never_the_token()
    {
        await _server.StartAsync(Ct);

        var response = await SendJsonAsync(
            HttpMethod.Post,
            "/api/plugins/sources",
            new
            {
                repository = "https://github.com/Diddyy/turbo-bugreport-plugin",
                follow = "latest",
                token = "ghp_secret",
            }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // The install it starts asks GitHub, which is not here: it ends at once.
        while (_sources.Job?.Status == PluginJobStatus.Running)
            await Task.Delay(20, Ct);

        var body = await (
            await SendAsync(HttpMethod.Get, "/api/plugins")
        ).Content.ReadAsStringAsync(Ct);
        var source = JsonDocument
            .Parse(body)
            .RootElement.GetProperty("sources")
            .EnumerateArray()
            .Single();

        body.Should().NotContain("ghp_secret");
        source.GetProperty("repository").GetString().Should().Be("Diddyy/turbo-bugreport-plugin");
        source.GetProperty("hasToken").GetBoolean().Should().BeTrue();
        Log().Should().Equal([("source add Diddyy/turbo-bugreport-plugin", "completed")]);
    }

    [Fact]
    public void The_install_node_is_never_granted_by_a_wildcard()
    {
        new CorePermissionNodeSource()
            .Nodes.Single(n => n.Node == PermissionNodes.Admin.PLUGINS_INSTALL)
            .ExplicitOnly.Should()
            .BeTrue();
    }

    private PluginAdministration Restart()
    {
        var options = Options.Create(
            new PluginConfig { PluginFolderPath = _folder, UploadEnabled = _uploadEnabled }
        );
        var manager = new PluginManager(
            new ServiceCollection().BuildServiceProvider(),
            new AssemblyProcessor([]),
            options,
            NullLogger<PluginManager>.Instance
        );

        var plugins = new PluginAdministration(manager, NullLogger<PluginAdministration>.Instance);

        // GitHub is not asked here (PluginSourcesTests asks a stand-in): every request fails.
        _sources = new PluginSources(
            manager,
            plugins,
            new PluginJobs(
                new Fakes().Create<IHostApplicationLifetime>(),
                TimeProvider.System,
                NullLogger<PluginJobs>.Instance
            ),
            new PluginSecrets(options, NullLogger<PluginSecrets>.Instance),
            new GitHubReleases(new HttpClient(new NoGitHub()), options),
            options,
            TimeProvider.System,
            NullLogger<PluginSources>.Instance
        );

        return plugins;
    }

    private sealed class NoGitHub : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private void WritePlugin(string key, string name)
    {
        var dir = Path.Combine(_folder, key);

        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "manifest.json"), Manifest(key, name));
        File.WriteAllText(Path.Combine(dir, "Sample.dll"), "MZ");
    }

    private static string Manifest(string key, string name) =>
        JsonSerializer.Serialize(
            new
            {
                Name = name,
                Key = key,
                Version = "1.2.3",
                Author = "Staff",
                AssemblyFile = "Sample.dll",
            }
        );

    private static byte[] Package(params (string Name, string Content)[] files)
    {
        using var memory = new MemoryStream();

        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (name, content) in files)
            {
                using var entry = zip.CreateEntry(name).Open();
                entry.Write(Encoding.UTF8.GetBytes(content));
            }

        return memory.ToArray();
    }

    private (string Arguments, string Outcome)[] Log()
    {
        using var db = _db.CreateDbContext();

        db.CommandLogs.Should().OnlyContain(x => x.Command == "plugin" && x.Source == "panel");

        return
        [
            .. db.CommandLogs.OrderBy(x => x.Id).ToList().Select(x => (x.Arguments, x.Outcome)),
        ];
    }

    private async Task<HttpResponseMessage> InstallAsync(byte[] package)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(package);

        file.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        form.Add(file, "file", "plugin.zip");

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"http://127.0.0.1:{_port}/api/plugins/install"
        )
        {
            Content = form,
        };

        request.Headers.Authorization = new("Bearer", "a-session");

        return await _client.SendAsync(request, Ct);
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
}
