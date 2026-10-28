using System.IO;
using System.IO.Compression;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Contracts.Plugins;
using Turbo.Database.Context;
using Turbo.Plugins;
using Turbo.Plugins.Configuration;
using Turbo.Plugins.Data;
using Turbo.Primitives.Plugins;
using Turbo.Runtime;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

[Trait("evidence", "inferred")]
public sealed class ThemeCreationTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-theme-create-" + Guid.NewGuid().ToString("N")
    );
    private readonly Fakes _fakes = new();
    private readonly CapturingLogger<PluginManager> _log = new(keepExceptions: false);
    private readonly ServiceProvider _host;
    private readonly PluginManager _manager;
    private readonly ThemeAuthoring _themes;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ThemeCreationTests()
    {
        var source = Path.Combine(_folder, ".source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "theme.json"), "{ \"extends\": \"habbo\" }");
        _fakes.Handlers["FolderOf"] = call => (string)call.Args[0]! == "source" ? source : null;
        _fakes.Handlers["ThemeOf"] = call => new PluginTheme((string)call.Args[0]!, "habbo");
        _fakes.Handlers["ExportAsync"] = _ => Task.FromResult<byte[]?>(Package("night"));
        _fakes.Handlers["Loaded"] = _ => new ActionDisposable(() => { });
        _host = new ServiceCollection()
            .AddSingleton(_fakes.Create<IPluginGamedataSeeds>())
            .AddSingleton(_fakes.Create<IPluginStyles>())
            .BuildServiceProvider();
        _manager = new PluginManager(
            _host,
            new AssemblyProcessor([]),
            Options.Create(
                new PluginConfig
                {
                    PluginFolderPath = _folder,
                    UploadEnabled = true,
                    DevPluginPaths = [Path.Combine(_folder, ".dev")],
                }
            ),
            _log
        );
        _themes = new ThemeAuthoring(
            _manager,
            _fakes.Create<IHotelDataPlugin>(),
            _host,
            NullLogger<ThemeAuthoring>.Instance
        );
    }

    public void Dispose()
    {
        _host.Dispose();
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Theory]
    [InlineData("night")]
    [InlineData("NIGHT")]
    public async Task Concurrent_creators_reserve_the_key_before_copying(string competingKey)
    {
        var copying = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exports = 0;
        async Task<byte[]?> ExportAsync()
        {
            if (Interlocked.Increment(ref exports) == 1)
            {
                copying.TrySetResult();
                await release.Task.WaitAsync(Ct);
            }
            return Package("night");
        }
        _fakes.Handlers["ExportAsync"] = _ => ExportAsync();
        var first = _themes.CreateAsync(Request("night", copy: true), Ct);
        await copying.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        try
        {
            var second = () => _themes.CreateAsync(Request(competingKey, copy: true), Ct);
            await second.Should().ThrowAsync<ArgumentException>().WithMessage("*already*");
            exports.Should().Be(1, "the competing request must be rejected before exporting");
        }
        finally
        {
            release.TrySetResult();
            await first;
        }
        _manager.Snapshot().Should().ContainSingle().Which.Key.Should().Be("night");
    }

    [Theory]
    [InlineData("night")]
    [InlineData("NIGHT")]
    [InlineData("other-folder")]
    [InlineData(".dev")]
    public async Task A_plugin_appearing_while_exporting_is_never_overwritten_or_deleted(
        string directory
    )
    {
        var copying = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<byte[]?> ExportAsync()
        {
            copying.TrySetResult();
            await release.Task.WaitAsync(Ct);
            return Package("night");
        }
        _fakes.Handlers["ExportAsync"] = _ => ExportAsync();
        var create = _themes.CreateAsync(Request("night", copy: true), Ct);
        await copying.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        var target = WritePlugin("night", "Already installed", directory);
        var manifest = await File.ReadAllTextAsync(Path.Combine(target, "manifest.json"), Ct);
        release.TrySetResult();
        var action = () => create.WaitAsync(Ct);
        await action.Should().ThrowAsync<ArgumentException>().WithMessage("*already*");
        (await File.ReadAllTextAsync(Path.Combine(target, "manifest.json"), Ct))
            .Should()
            .Be(manifest);
        AssertNoStaging();
    }

    [Fact]
    public async Task Reload_failure_never_deletes_a_folder_that_replaced_this_operation_s_output()
    {
        var failure = new InvalidOperationException("seed failed");
        var replaced = false;
        _fakes.Handlers["Loaded"] = call =>
        {
            if (replaced)
                return new ActionDisposable(() => { });
            replaced = true;
            var target = Path.Combine(_folder, "night");
            Directory.Delete(target, recursive: true);
            WritePlugin("night", "Replacement");
            File.WriteAllText(Path.Combine(target, "winner.txt"), "keep me");
            throw failure;
        };
        var create = () => _themes.CreateAsync(Request("night"), Ct);
        (await create.Should().ThrowAsync<InvalidOperationException>())
            .Which.Should()
            .BeSameAs(failure);
        (await File.ReadAllTextAsync(Path.Combine(_folder, "night", "winner.txt"), Ct))
            .Should()
            .Be("keep me");
        _manager.Snapshot().Should().ContainSingle().Which.Name.Should().Be("Replacement");
        AssertNoStaging();
    }

    [Fact]
    public async Task Staged_files_stay_out_of_discovery_until_the_complete_theme_is_published()
    {
        WritePlugin("blocker", "Blocker");
        var loading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLoad = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        async Task SeedAsync(string key)
        {
            if (key != "blocker")
                return;
            loading.TrySetResult();
            await releaseLoad.Task.WaitAsync(Ct);
        }
        _fakes.Handlers["SeedAsync"] = call => SeedAsync((string)call.Args[0]!);
        var scan = _manager.LoadAllAsync(ct: Ct);
        await loading.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        var copying = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCopy = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        async Task<byte[]?> ExportAsync()
        {
            copying.TrySetResult();
            await releaseCopy.Task.WaitAsync(Ct);
            return Package("night");
        }
        _fakes.Handlers["ExportAsync"] = _ => ExportAsync();
        var create = _themes.CreateAsync(Request("night", copy: true), Ct);
        try
        {
            await copying.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
            _manager.Refresh();
            _manager
                .Snapshot()
                .Select(x => x.Key)
                .Should()
                .Equal(["blocker"], "the unfinished stage is not a plugin or an unreadable folder");
            releaseCopy.TrySetResult();
            var staging = await ReadyStageAsync();
            Directory.Exists(Path.Combine(_folder, "night")).Should().BeFalse();
            PluginHotReloadService
                .DataPluginFolderOf(Path.Combine(staging, "manifest.json"), _folder, [])
                .Should()
                .BeNull();
            _manager.Refresh();
            _manager.Snapshot().Select(x => x.Key).Should().Equal("blocker");
        }
        finally
        {
            releaseCopy.TrySetResult();
            releaseLoad.TrySetResult();
            await scan;
            await create;
        }
        var target = Path.Combine(_folder, "night");
        File.Exists(Path.Combine(target, "theme.json")).Should().BeTrue();
        File.Exists(Path.Combine(target, "wwwroot", "template-patches.json")).Should().BeTrue();
        Directory.EnumerateDirectories(target, ".theme-creation-*").Should().BeEmpty();
        _manager.Snapshot().Single(x => x.Key == "night").State.Should().Be(PluginStateType.Loaded);
        AssertNoStaging();
    }

    [Fact]
    public async Task Cancellation_while_waiting_to_publish_cleans_the_stage_and_releases_the_key()
    {
        WritePlugin("blocker", "Blocker");
        var loading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task SeedAsync()
        {
            loading.TrySetResult();
            await release.Task.WaitAsync(Ct);
        }
        _fakes.Handlers["SeedAsync"] = _ => SeedAsync();
        var scan = _manager.LoadAllAsync(ct: Ct);
        await loading.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var create = _themes.CreateAsync(Request("night"), cancelled.Token);
        try
        {
            await ReadyStageAsync();
            await cancelled.CancelAsync();
            var action = () => create.WaitAsync(TimeSpan.FromSeconds(10), Ct);
            await action.Should().ThrowAsync<OperationCanceledException>();
            Directory.Exists(Path.Combine(_folder, "night")).Should().BeFalse();
            AssertNoStaging();
        }
        finally
        {
            release.TrySetResult();
            await scan;
        }
        _fakes.Handlers.TryRemove("SeedAsync", out _);
        await _themes.CreateAsync(Request("night"), Ct);
        _manager.Snapshot().Single(x => x.Key == "night").State.Should().Be(PluginStateType.Loaded);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_or_cancelled_reload_retracts_only_its_publication_and_allows_retry(
        bool cancellation
    )
    {
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        Exception failure = cancellation
            ? new OperationCanceledException(cancelled.Token)
            : new InvalidOperationException("load failed");
        _fakes.Handlers["Loaded"] = _ =>
        {
            if (!cancellation)
                throw failure;
            cancelled.Cancel();
            return new ActionDisposable(() => { });
        };
        var create = () => _themes.CreateAsync(Request("night"), cancelled.Token);
        var thrown = (await create.Should().ThrowAsync<Exception>()).Which;
        if (cancellation)
            thrown
                .Should()
                .BeOfType<OperationCanceledException>()
                .Which.CancellationToken.Should()
                .Be(cancelled.Token);
        else
            thrown.Should().BeSameAs(failure);
        Directory.Exists(Path.Combine(_folder, "night")).Should().BeFalse();
        _manager.Snapshot().Should().BeEmpty();
        AssertNoStaging();
        _fakes.Handlers["Loaded"] = _ => new ActionDisposable(() => { });
        await _themes.CreateAsync(Request("night"), Ct);
        _manager
            .Snapshot()
            .Should()
            .ContainSingle()
            .Which.State.Should()
            .Be(PluginStateType.Loaded);
    }

    [Fact]
    public async Task A_partial_copy_failure_leaves_no_final_folder_and_can_be_retried()
    {
        _fakes.Handlers["ExportAsync"] = _ =>
            Task.FromResult<byte[]?>(
                Package("night", ("wwwroot/styles", "a file cannot replace the styles directory"))
            );
        var create = () => _themes.CreateAsync(Request("night", copy: true), Ct);
        await create
            .Should()
            .ThrowAsync<Exception>()
            .Where(x => x is IOException || x is UnauthorizedAccessException);
        Directory.Exists(Path.Combine(_folder, "night")).Should().BeFalse();
        _manager.Snapshot().Should().BeEmpty();
        AssertNoStaging();
        await _themes.CreateAsync(Request("night"), Ct);
        _manager
            .Snapshot()
            .Should()
            .ContainSingle()
            .Which.State.Should()
            .Be(PluginStateType.Loaded);
    }

    [Fact]
    public async Task Reconciliation_failure_does_not_mask_the_original_error_or_delete_a_replacement()
    {
        var original = new InvalidOperationException("original load failed");
        var reconciliation = new IOException("reconciliation failed");
        var calls = 0;
        _fakes.Handlers["Loaded"] = _ =>
        {
            if (++calls > 1)
                throw reconciliation;
            Directory.Delete(Path.Combine(_folder, "night"), recursive: true);
            WritePlugin("night", "Replacement");
            throw original;
        };
        var create = () => _themes.CreateAsync(Request("night"), Ct);
        (await create.Should().ThrowAsync<InvalidOperationException>())
            .Which.Should()
            .BeSameAs(original);
        Directory.Exists(Path.Combine(_folder, "night")).Should().BeTrue();
        _log.Entries.Should()
            .Contain(x => x.Message.Contains("Could not reconcile", StringComparison.Ordinal));
        AssertNoStaging();
        _fakes.Handlers["Loaded"] = _ => new ActionDisposable(() => { });
        (await _manager.TryRemoveAsync("night", Ct)).Should().BeTrue();
        await _manager.LoadChangedAsync(Ct);
        await _themes.CreateAsync(Request("night"), Ct);
        _manager
            .Snapshot()
            .Should()
            .ContainSingle()
            .Which.State.Should()
            .Be(PluginStateType.Loaded);
    }

    [Fact]
    public async Task Package_installation_cannot_replace_a_theme_whose_creation_is_in_progress()
    {
        var copying = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<byte[]?> ExportAsync()
        {
            copying.TrySetResult();
            await release.Task.WaitAsync(Ct);
            return Package("night");
        }
        _fakes.Handlers["ExportAsync"] = _ => ExportAsync();
        var create = _themes.CreateAsync(Request("night", copy: true), Ct);
        await copying.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        var admin = new PluginAdministration(
            _manager,
            new PluginTables(
                _fakes.Create<IDbContextFactory<TurboDbContext>>(),
                NullLogger<PluginTables>.Instance
            ),
            [],
            NullLogger<PluginAdministration>.Instance
        );
        try
        {
            using var package = new MemoryStream(Package("NIGHT"));
            var install = await admin.InstallAsync(package, _ => null, Ct);
            install.Ok.Should().BeFalse();
            install.Message.Should().Contain("already being published");
            Directory.Exists(Path.Combine(_folder, "NIGHT")).Should().BeFalse();
        }
        finally
        {
            release.TrySetResult();
            await create;
        }
        _manager.Snapshot().Should().ContainSingle().Which.Key.Should().Be("night");
        AssertNoStaging();
    }

    [Fact]
    public async Task Theme_creation_respects_an_installer_s_reservation_and_can_retry_after_refusal()
    {
        var admin = new PluginAdministration(
            _manager,
            new PluginTables(
                _fakes.Create<IDbContextFactory<TurboDbContext>>(),
                NullLogger<PluginTables>.Instance
            ),
            [],
            NullLogger<PluginAdministration>.Instance
        );
        Task<PluginTheme>? creation = null;
        using var package = new MemoryStream(Package("night"));
        var install = await admin.InstallAsync(
            package,
            _ =>
            {
                creation = _themes.CreateAsync(Request("NIGHT"), Ct);
                return "Refused for this test";
            },
            Ct
        );
        install.Ok.Should().BeFalse();
        creation.Should().NotBeNull();
        var create = () => creation!.WaitAsync(Ct);
        await create.Should().ThrowAsync<ArgumentException>().WithMessage("*already*");
        Directory.Exists(Path.Combine(_folder, "night")).Should().BeFalse();
        await _themes.CreateAsync(Request("night"), Ct);
        _manager
            .Snapshot()
            .Should()
            .ContainSingle()
            .Which.State.Should()
            .Be(PluginStateType.Loaded);
        AssertNoStaging();
    }

    [Fact]
    public async Task Theme_metadata_failure_is_reported_before_publication_and_can_be_retried()
    {
        var failure = new ArgumentException("Invalid theme metadata");
        _fakes.Handlers["ThemeOf"] = _ => throw failure;
        var create = () => _themes.CreateAsync(Request("night"), Ct);
        (await create.Should().ThrowAsync<ArgumentException>()).Which.Should().BeSameAs(failure);
        Directory.Exists(Path.Combine(_folder, "night")).Should().BeFalse();
        AssertNoStaging();
        _fakes.Handlers["ThemeOf"] = call => new PluginTheme((string)call.Args[0]!, "habbo");
        await _themes.CreateAsync(Request("night"), Ct);
        _manager
            .Snapshot()
            .Should()
            .ContainSingle()
            .Which.State.Should()
            .Be(PluginStateType.Loaded);
    }

    private async Task<string> ReadyStageAsync()
    {
        var root = Path.Combine(_folder, ".theme-staging");
        for (var attempt = 0; attempt < 1000; attempt++)
        {
            var staging = Directory.Exists(root)
                ? Directory
                    .EnumerateDirectories(root)
                    .FirstOrDefault(x =>
                        File.Exists(Path.Combine(x, "wwwroot", "template-patches.json"))
                    )
                : null;
            if (staging is not null)
                return staging;
            await Task.Delay(10, Ct);
        }
        throw new TimeoutException("The theme did not finish staging.");
    }

    private static ThemeCreateRequest Request(string key, bool copy = false) =>
        new(key, "Created theme", null, copy ? "source" : null, null);

    private string WritePlugin(string key, string name, string? directory = null)
    {
        var folder = Path.Combine(_folder, directory ?? key);
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            Path.Combine(folder, "manifest.json"),
            $$"""{ "Name": "{{name}}", "Key": "{{key}}", "Version": "1.0.0", "Author": "Staff" }"""
        );
        return folder;
    }

    private void AssertNoStaging()
    {
        var staging = Path.Combine(_folder, ".theme-staging");
        if (Directory.Exists(staging))
            Directory.EnumerateFileSystemEntries(staging).Should().BeEmpty();
    }

    private static byte[] Package(string key, params (string Path, string Content)[] extras)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (
                var writer = new StreamWriter(
                    zip.CreateEntry("manifest.json").Open(),
                    Encoding.UTF8
                )
            )
                writer.Write(
                    $$"""{ "Name": "Created theme", "Key": "{{key}}", "Version": "1.0.0", "Author": "Staff" }"""
                );
            foreach (var (path, content) in extras)
            {
                using var writer = new StreamWriter(zip.CreateEntry(path).Open(), Encoding.UTF8);
                writer.Write(content);
            }
        }
        return memory.ToArray();
    }
}
