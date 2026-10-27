using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Contracts.Plugins;
using Turbo.Plugins;
using Turbo.Plugins.Configuration;
using Turbo.Plugins.Data;
using Turbo.Plugins.Sources;
using Turbo.Primitives.Plugins;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// Plugins linked to a GitHub repository, over the real plugin manager, a plugin folder on disk,
/// real plugin sources and a stand-in for GitHub's releases API: linking installs the release's
/// sources, a check shows a newer one, an update installs it and keeps the version it replaced to
/// roll back to, an update that does not load is put back, and sources with an executable file,
/// or written for another plugin API, change nothing. A token is sealed on disk and sent to GitHub
/// only.
/// </summary>
public sealed class PluginSourcesTests : IDisposable
{
    private const string API = "https://api.github.test";
    private const string REPO = "Diddyy/turbo-sample-plugin";

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-plugin-sources-" + Guid.NewGuid().ToString("N")
    );
    private readonly FakeGitHub _github = new();

    private PluginConfig _config;
    private PluginAdministration _plugins = null!;
    private PluginSources _sources = null!;

    public PluginSourcesTests()
    {
        Directory.CreateDirectory(_folder);
        _config = new PluginConfig
        {
            PluginFolderPath = _folder,
            UploadEnabled = true,
            GitHubApiUrl = API,
        };
        Build();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException) { }
    }

    [Fact]
    public async Task Linking_installs_the_release_and_a_newer_one_is_offered_then_installed_keeping_the_old()
    {
        _github.Release("v1.0.0", Package("1.0.0"));

        var linked = _sources.Add(Input(), player: 1);

        linked.Ok.Should().BeTrue(linked.Message);
        (await JobAsync()).Status.Should().Be(PluginJobStatus.Done);
        Loaded().Version.Should().Be("1.0.0");

        var source = _sources.List().Single();
        source.Key.Should().Be("sample");
        source.InstalledTag.Should().Be("v1.0.0");
        source.UpdateAvailable.Should().BeFalse();

        _github.Release("v1.0.1", Package("1.0.1"));
        _sources.Check(source.Id, player: 1)!.Ok.Should().BeTrue();

        var check = await JobAsync();
        check.Result.Should().Be("1 update available.");
        _sources
            .List()
            .Single()
            .Should()
            .Match<PluginSourceSnapshot>(x =>
                x.UpdateAvailable && x.LatestTag == "v1.0.1" && x.InstalledTag == "v1.0.0"
            );

        _sources.Update(source.Id, player: 1)!.Ok.Should().BeTrue();

        var update = await JobAsync();
        update.Status.Should().Be(PluginJobStatus.Done, update.Error);
        update.Done.Should().Be(update.Total);
        Loaded().Version.Should().Be("1.0.1");
        _sources.List().Single().UpdateAvailable.Should().BeFalse();
        _plugins.VersionsOf("sample").Should().Equal("1.0.0");
    }

    [Fact]
    public async Task Rolling_back_puts_the_kept_version_back_and_offers_the_release_again()
    {
        await InstallTwoAsync();

        var result = await _plugins.RollbackAsync("sample", "1.0.0", _ => null, Ct);

        result!.Ok.Should().BeTrue(result.Message);
        Loaded().Version.Should().Be("1.0.0");
        _plugins.VersionsOf("sample").Should().Equal("1.0.1");

        var source = _sources.List().Single();
        source.UpdateAvailable.Should().BeTrue();
        source.InstalledVersion.Should().Be("1.0.0");

        (await _plugins.RollbackAsync("sample", "../../etc", _ => null, Ct)).Should().BeNull();
    }

    [Fact]
    public async Task An_automatic_update_leaves_a_release_rolled_back_from_alone()
    {
        _github.Release("v1.0.0", Package("1.0.0"));
        _sources.Add(Input() with { AutoUpdate = true }, player: 1);
        await JobAsync();

        _github.Release("v1.0.1", Package("1.0.1"));
        await _sources.CheckAllAsync(Ct);
        (await JobAsync()).Status.Should().Be(PluginJobStatus.Done);
        Loaded().Version.Should().Be("1.0.1");

        await _plugins.RollbackAsync("sample", "1.0.0", _ => null, Ct);
        var jobs = _github.Downloads;
        await _sources.CheckAllAsync(Ct);

        _github.Downloads.Should().Be(jobs);
        Loaded().Version.Should().Be("1.0.0");
        _sources.List().Single().UpdateAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task A_scheduled_check_only_offers_an_update_unless_the_link_updates_on_its_own()
    {
        _github.Release("v1.0.0", Package("1.0.0"));
        _sources.Add(Input(), player: 1);
        await JobAsync();

        _github.Release("v1.0.1", Package("1.0.1"));
        await _sources.CheckAllAsync(Ct);

        _sources.List().Single().UpdateAvailable.Should().BeTrue();
        Loaded().Version.Should().Be("1.0.0");
        _github.Downloads.Should().Be(1);
    }

    [Fact]
    public async Task An_update_that_does_not_load_is_put_back()
    {
        _github.Release("v1.0.0", Package("1.0.0"));
        _sources.Add(Input(), player: 1);
        await JobAsync();

        _github.Release("v2.0.0", Package("2.0.0", failsToStart: true));
        _sources.Update(_sources.List().Single().Id, player: 1);

        var update = await JobAsync();
        update.Status.Should().Be(PluginJobStatus.Failed);
        update.Error.Should().Contain("2.0.0 did not load").And.Contain("Put back 1.0.0");
        Loaded().Version.Should().Be("1.0.0");
        _sources.List().Single().InstalledTag.Should().Be("v1.0.0");
    }

    /// <summary>
    /// A plugin is installed as its sources at the release's tag: an assembly in them (one built
    /// and committed, or attached as the plugin) is refused and nothing is installed.
    /// </summary>
    [Fact]
    public async Task Sources_with_an_executable_file_change_nothing()
    {
        _github.Release("v1.0.0", Package("1.0.0", extra: ("server/bin/Sample.dll", "MZ")));
        _sources.Add(Input(), player: 1);

        var job = await JobAsync();

        job.Status.Should().Be(PluginJobStatus.Failed);
        job.Error.Should().Contain("server/bin/Sample.dll is a .dll file");
        Directory.Exists(Path.Combine(_folder, "sample")).Should().BeFalse();
    }

    /// <summary>
    /// A repository whose plugin is in a folder of it (a solution with the plugin's project and its
    /// tests) is installed from the folder the link names, with that folder's files only.
    /// </summary>
    [Fact]
    public async Task A_plugin_in_a_folder_of_the_repository_installs_from_the_folder_the_link_names()
    {
        _github.Release("v1.0.0", Package("1.0.0", folder: "Sample"));
        _sources.Add(Input() with { Folder = "Sample" }, player: 1);

        (await JobAsync()).Status.Should().Be(PluginJobStatus.Done);
        Loaded().Version.Should().Be("1.0.0");
        File.Exists(Path.Combine(_folder, "sample", "server", "SamplePlugin.cs")).Should().BeTrue();
        File.Exists(Path.Combine(_folder, "sample", "README.md"))
            .Should()
            .BeFalse("the repository's own files are not the plugin's");
    }

    [Fact]
    public async Task A_plugin_built_for_another_plugin_api_is_refused()
    {
        _github.Release("v1.0.0", Package("1.0.0", apiVersion: PluginApi.VERSION + 1));
        _sources.Add(Input(), player: 1);

        var job = await JobAsync();

        job.Status.Should().Be(PluginJobStatus.Failed);
        job.Error.Should()
            .Contain($"built for plugin API {PluginApi.VERSION + 1}")
            .And.Contain($"this server has plugin API {PluginApi.VERSION}");
        Directory.Exists(Path.Combine(_folder, "sample")).Should().BeFalse();
    }

    [Fact]
    public async Task A_token_is_sealed_on_disk_sent_to_github_and_never_shown()
    {
        _github.Release("v1.0.0", Package("1.0.0"));
        _sources.Add(Input() with { Token = "ghp_secret" }, player: 1);
        await JobAsync();

        (await File.ReadAllTextAsync(Path.Combine(_folder, "plugins.json"), Ct))
            .Should()
            .NotContain("ghp_secret");
        _github.Authorizations.Should().OnlyContain(x => x == "Bearer ghp_secret");
        _sources.List().Single().HasToken.Should().BeTrue();

        // Saved without a token: the one saved stays; empty removes it.
        var id = _sources.List().Single().Id;
        _sources.Edit(id, Input() with { Token = null })!.Source!.HasToken.Should().BeTrue();
        _sources.Edit(id, Input() with { Token = "" })!.Source!.HasToken.Should().BeFalse();
    }

    [Fact]
    public void Only_allowed_owners_and_github_repositories_can_be_linked()
    {
        _config = new PluginConfig
        {
            PluginFolderPath = _folder,
            UploadEnabled = true,
            GitHubApiUrl = API,
            AllowedSources = ["nitrodevco"],
        };
        Build();

        _sources.Add(Input(), player: 1).Message.Should().Contain("can't be linked from Diddyy");
        _sources.Add(Input() with { Repository = "not a repo" }, player: 1).Ok.Should().BeFalse();
        _sources.List().Should().BeEmpty();
    }

    [Fact]
    public async Task A_private_repository_without_a_token_says_so()
    {
        _sources.Add(Input(), player: 1);

        var job = await JobAsync();

        job.Error.Should().Contain("private: add a token");
        _sources.List().Single().Error.Should().Contain("private: add a token");
    }

    [Theory]
    [InlineData(
        "https://github.com/Diddyy/turbo-bugreport-plugin",
        "Diddyy/turbo-bugreport-plugin"
    )]
    [InlineData(
        "https://github.com/Diddyy/turbo-bugreport-plugin.git",
        "Diddyy/turbo-bugreport-plugin"
    )]
    [InlineData("github.com/Diddyy/turbo-bugreport-plugin/", "Diddyy/turbo-bugreport-plugin")]
    [InlineData("Diddyy/turbo-bugreport-plugin", "Diddyy/turbo-bugreport-plugin")]
    [InlineData("https://gitlab.com/Diddyy/turbo-bugreport-plugin", null)]
    [InlineData("Diddyy/../x", null)]
    [InlineData("Diddyy", null)]
    public void A_repository_is_read_from_its_url_or_owner_and_name(string input, string? expected)
    {
        GitHubReleases.ParseRepository(input).Should().Be(expected);
    }

    private void Build()
    {
        var options = Options.Create(_config);
        var manager = new PluginManager(
            new ServiceCollection().BuildServiceProvider(),
            new AssemblyProcessor([]),
            options,
            NullLogger<PluginManager>.Instance
        );

        var tables = new PluginTables(new SqliteDb(), NullLogger<PluginTables>.Instance);
        _plugins = new PluginAdministration(
            manager,
            tables,
            [],
            NullLogger<PluginAdministration>.Instance
        );
        _sources = new PluginSources(
            manager,
            _plugins,
            new PluginJobs(
                new Fakes().Create<IHostApplicationLifetime>(),
                TimeProvider.System,
                NullLogger<PluginJobs>.Instance
            ),
            new PluginSecrets(options, NullLogger<PluginSecrets>.Instance),
            new GitHubReleases(new HttpClient(_github), options),
            options,
            TimeProvider.System,
            NullLogger<PluginSources>.Instance
        );
    }

    private static PluginSourceInput Input() =>
        new($"https://github.com/{REPO}", "latest", null, null, null, false);

    private async Task InstallTwoAsync()
    {
        _github.Release("v1.0.0", Package("1.0.0"));
        _sources.Add(Input(), player: 1);
        await JobAsync();
        _github.Release("v1.0.1", Package("1.0.1"));
        _sources.Update(_sources.List().Single().Id, player: 1);
        (await JobAsync()).Status.Should().Be(PluginJobStatus.Done);
    }

    private PluginSnapshot Loaded()
    {
        var plugin = _plugins.List().Single(p => p.Key == "sample");

        plugin.State.Should().Be(PluginStateType.Loaded, plugin.Error);

        return plugin;
    }

    private async Task<PluginJobSnapshot> JobAsync()
    {
        for (var i = 0; i < 300; i++)
        {
            if (_sources.Job is { Status: not PluginJobStatus.Running } job)
                return job;

            await Task.Delay(50, Ct);
        }

        throw new TimeoutException("The plugin job did not finish.");
    }

    /// <summary>
    /// The repository's sources at a tag, as GitHub zips them: under one top folder, the plugin's
    /// manifest and source in <paramref name="folder"/> (the root when null) and a README.
    /// </summary>
    private static byte[] Package(
        string version,
        bool failsToStart = false,
        int? apiVersion = null,
        string? folder = null,
        (string Path, string Content)? extra = null
    )
    {
        var manifest = JsonSerializer.Serialize(
            new
            {
                Name = "Sample",
                Key = "sample",
                Version = version,
                Author = "Staff",
                ApiVersion = apiVersion ?? PluginApi.VERSION,
            }
        );
        var plugin = $"Diddyy-turbo-sample-plugin-0123abc/{(folder is null ? "" : folder + "/")}";
        var files = new List<(string Path, string Content)>
        {
            ("Diddyy-turbo-sample-plugin-0123abc/README.md", "# Sample"),
            (plugin + "manifest.json", manifest),
            (plugin + "server/SamplePlugin.cs", Source(version, failsToStart)),
        };

        if (extra is { } file)
            files.Add((plugin + file.Path, file.Content));

        using var memory = new MemoryStream();

        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (path, content) in files)
            {
                using var entry = zip.CreateEntry(path).Open();

                entry.Write(Encoding.UTF8.GetBytes(content));
            }

        return memory.ToArray();
    }

    private static string Source(string version, bool failsToStart) =>
        $$"""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using Turbo.Contracts.Plugins;
            using Turbo.Contracts.Plugins.Exports;

            public sealed class SamplePlugin : ITurboPlugin
            {
                public string Key => "sample";
                public string Version => "{{version}}";
                public void ConfigureServices(IServiceCollection services, PluginManifest manifest) { }
                public Task StartAsync(IServiceProvider services, CancellationToken ct) =>
                    {{(
            failsToStart ? "throw new InvalidOperationException(\"broken build\")" : "Task.CompletedTask"
        )}};
                public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
                public Task BindExportsAsync(IExportBinder binder, IServiceProvider services) => Task.CompletedTask;
                public ValueTask DisposeAsync() => ValueTask.CompletedTask;
            }
            """;

    /// <summary>
    /// GitHub's releases API for <see cref="REPO"/>: the newest release, and the sources at its tag;
    /// without a release, 404 as for a private repository.
    /// </summary>
    private sealed class FakeGitHub : HttpMessageHandler
    {
        private string? _tag;
        private byte[] _sources = [];

        public List<string> Authorizations { get; } = [];

        public int Downloads { get; private set; }

        public void Release(string tag, byte[] sources)
        {
            _tag = tag;
            _sources = sources;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            if (request.Headers.Authorization is { } auth)
                Authorizations.Add(auth.ToString());

            var path = request.RequestUri!.AbsolutePath;

            if (_tag is null)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

            if (path == $"/repos/{REPO}/releases/latest")
                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            JsonSerializer.Serialize(
                                new
                                {
                                    tag_name = _tag,
                                    html_url = $"https://github.com/{REPO}/releases/tag/{_tag}",
                                    published_at = "2026-10-17T16:00:00Z",
                                }
                            )
                        ),
                    }
                );

            if (path == $"/repos/{REPO}/zipball/{_tag}")
            {
                Downloads++;

                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(_sources),
                    }
                );
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
