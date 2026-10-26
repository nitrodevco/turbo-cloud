using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Entities.Permissions;
using Turbo.Database.Entities.Players;
using Turbo.Players.Permissions;
using Turbo.Plugins;
using Turbo.Plugins.Configuration;
using Turbo.Plugins.Data;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains.Permissions;
using Turbo.Primitives.Plugins;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// Uninstalling a plugin from the panel (<see cref="IPluginAdministration.UninstallAsync"/>) over
/// the real plugin manager, a plugin folder on disk and a relational database: the dry run lists
/// its folder, kept versions, links, tables with their rows and grants, and changes nothing;
/// keeping its data takes only its code and links, and a reinstall finds the data again; deleting
/// it drops only the tables carrying its prefix, never the hotel's or another plugin's, and takes
/// its grants away through the permission grains.
/// </summary>
public sealed class PluginUninstallTests : IDisposable
{
    private static readonly PlayerId STAFF = new(7);

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-plugin-uninstall-" + Guid.NewGuid().ToString("N")
    );
    private readonly PluginManager _manager;
    private readonly PluginAdministration _plugins;

    public PluginUninstallTests()
    {
        Directory.CreateDirectory(_folder);

        _manager = new PluginManager(
            new ServiceCollection().BuildServiceProvider(),
            new AssemblyProcessor([]),
            Options.Create(new PluginConfig { PluginFolderPath = _folder, UploadEnabled = true }),
            NullLogger<PluginManager>.Instance
        );

        var tables = new PluginTables(_db, NullLogger<PluginTables>.Instance);

        _plugins = new PluginAdministration(
            _manager,
            tables,
            [tables, new PluginPermissionData(_db, _fakes.Create<IGrainFactory>())],
            NullLogger<PluginAdministration>.Instance
        );
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _db.Dispose();

        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task The_dry_run_lists_what_would_go_and_changes_nothing()
    {
        await GivenSampleWithDataAsync();

        var plan = (await _plugins.PlanUninstallAsync("sample", Ct))!;

        plan.Refusal.Should().BeNull();
        plan.Notes.Should().ContainSingle().Which.Should().Contain("not loaded");
        plan.Files.Should().Equal("sample/", ".versions/sample/0.9.0.zip");
        plan.Sources.Should().Equal("Staff/sample-plugin");
        plan.Data.Where(x => x.Kind == PluginDataKind.Table)
            .Select(x => (x.Name, x.Count))
            .Should()
            .BeEquivalentTo([("__EFMigrationsHistory_sample", 1L), ("sample_reports", 2L)]);
        plan.Data.Where(x => x.Kind == PluginDataKind.Grant)
            .Select(x => (x.Name, x.Count, x.Detail))
            .Should()
            .Equal(
                ("plugin.sample.view", 1L, "the group staff"),
                ("sample.*", 1L, "1 player"),
                ("sample.command.report", 1L, "the group staff")
            );

        Directory.Exists(Path.Combine(_folder, "sample")).Should().BeTrue();
        (await TablesAsync()).Should().Contain("sample_reports");
        _fakes.Log.Of("UnsetNodeAsync").Should().BeEmpty();
    }

    [Fact]
    public async Task Keeping_the_data_takes_the_code_and_links_and_a_reinstall_finds_the_data_again()
    {
        await GivenSampleWithDataAsync();

        var result = (
            await _plugins.UninstallAsync("sample", PluginUninstallMode.KeepData, STAFF, Ct)
        )!;

        result.Ok.Should().BeTrue(result.Message);
        Directory.Exists(Path.Combine(_folder, "sample")).Should().BeFalse();
        Directory.Exists(Path.Combine(_folder, ".versions", "sample")).Should().BeFalse();
        (await File.ReadAllTextAsync(Path.Combine(_folder, "plugins.json"), Ct))
            .Should()
            .NotContain("sample-plugin");
        _plugins.List().Should().NotContain(x => x.Key == "sample");
        (await TablesAsync()).Should().Contain(["sample_reports", "__EFMigrationsHistory_sample"]);
        _fakes.Log.Of("UnsetNodeAsync").Should().BeEmpty();

        await using (var package = new MemoryStream(Package("sample", "sample_")))
            await _plugins.InstallAsync(package, _ => null, Ct);

        Directory.Exists(Path.Combine(_folder, "sample")).Should().BeTrue();
        (await _plugins.PlanUninstallAsync("sample", Ct))!
            .Data.Should()
            .Contain(x => x.Name == "sample_reports" && x.Count == 2);
    }

    [Fact]
    public async Task Deleting_the_data_drops_only_its_own_tables_and_takes_its_grants_away()
    {
        await GivenSampleWithDataAsync();

        var result = (
            await _plugins.UninstallAsync("sample", PluginUninstallMode.DeleteData, STAFF, Ct)
        )!;

        result.Ok.Should().BeTrue(result.Message);
        result.Message.Should().Contain("2 tables (3 rows)").And.Contain("3 grants");

        var tables = await TablesAsync();

        tables.Should().NotContain(["sample_reports", "__EFMigrationsHistory_sample"]);
        // Another plugin's tables, whose prefix starts with this one's, and the hotel's stay.
        tables.Should().Contain(["sample_extra_items", "players", "permission_group_nodes"]);

        var group = _fakes
            .Log.Of("UnsetNodeAsync")
            .Where(x => x.Interface == typeof(IPermissionGroupDirectoryGrain))
            .Select(x => ((string)x.Args[0]!, (string)x.Args[1]!, (PlayerId?)x.Args[3]));
        var player = _fakes
            .Log.Of("UnsetNodeAsync")
            .Where(x => x.Interface == typeof(IPlayerPermissionGrain))
            .Select(x => (x.Key, (string)x.Args[0]!));

        group
            .Should()
            .BeEquivalentTo([
                ("staff", "plugin.sample.view", (PlayerId?)STAFF),
                ("staff", "sample.command.report", (PlayerId?)STAFF),
            ]);
        player.Should().Equal(((object?)1L, "sample.*"));
        // Core's nodes, and a root that only starts with the key's letters, are not the plugin's.
        _fakes
            .Log.Calls.Should()
            .NotContain(x =>
                x.Args.OfType<string>().Any(a => a == "room.enter" || a == "samplex.y")
            );
    }

    [Fact]
    public async Task A_prefix_that_matches_the_hotels_tables_drops_none_of_them()
    {
        WritePlugin("hotelish", "player");
        await _manager.LoadAllAsync(ct: Ct);

        (await _plugins.PlanUninstallAsync("hotelish", Ct))!
            .Data.Where(x => x.Kind == PluginDataKind.Table)
            .Should()
            .BeEmpty();

        var result = (
            await _plugins.UninstallAsync("hotelish", PluginUninstallMode.DeleteData, STAFF, Ct)
        )!;

        result.Ok.Should().BeTrue(result.Message);

        (await TablesAsync()).Should().Contain(["players", "player_badges"]);
    }

    [Fact]
    public async Task A_plugin_another_installed_one_depends_on_is_not_uninstalled()
    {
        WritePlugin("base", "base_");
        WritePlugin("addon", "addon_", dependsOn: "base");
        await _manager.LoadAllAsync(ct: Ct);

        var plan = (await _plugins.PlanUninstallAsync("base", Ct))!;
        var result = (
            await _plugins.UninstallAsync("base", PluginUninstallMode.KeepData, STAFF, Ct)
        )!;

        plan.Refusal.Should().Contain("addon");
        result.Ok.Should().BeFalse();
        Directory.Exists(Path.Combine(_folder, "base")).Should().BeTrue();
    }

    [Fact]
    public async Task The_export_writes_its_tables_and_rows_as_sql()
    {
        await GivenSampleWithDataAsync();

        using var output = new MemoryStream();

        (await _plugins.ExportDataAsync("sample", output, Ct)).Should().BeTrue();

        var sql = Encoding.UTF8.GetString(output.ToArray());

        sql.Should().Contain("DROP TABLE IF EXISTS \"sample_reports\";");
        sql.Should().Contain("CREATE TABLE sample_reports");
        sql.Should()
            .Contain(
                "INSERT INTO \"sample_reports\" (\"id\", \"text\") VALUES (1, 'it''s broken');"
            );
        sql.Should().NotContain("sample_extra_items");
        (await _plugins.ExportDataAsync("nobody", output, Ct)).Should().BeFalse();
    }

    /// <summary>
    /// The plugin "sample" (prefix <c>sample_</c>) with two rows, an applied migration, a kept
    /// version, a link and grants; beside it "extra" (prefix <c>sample_extra_</c>) with its own table.
    /// </summary>
    private async Task GivenSampleWithDataAsync()
    {
        WritePlugin("sample", "sample_");
        WritePlugin("extra", "sample_extra_");
        Directory.CreateDirectory(Path.Combine(_folder, ".versions", "sample"));
        await File.WriteAllBytesAsync(
            Path.Combine(_folder, ".versions", "sample", "0.9.0.zip"),
            [1],
            Ct
        );
        await File.WriteAllTextAsync(
            Path.Combine(_folder, "plugins.json"),
            """{ "Sources": [ { "Id": "s1", "Repository": "Staff/sample-plugin", "Key": "sample" } ] }""",
            Ct
        );

        await using (var db = await _db.CreateDbContextAsync(Ct))
        {
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TABLE sample_reports (id INTEGER PRIMARY KEY, text TEXT)",
                Ct
            );
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO sample_reports VALUES (1, 'it''s broken'), (2, NULL)",
                Ct
            );
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TABLE __EFMigrationsHistory_sample (MigrationId TEXT PRIMARY KEY, ProductVersion TEXT)",
                Ct
            );
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO __EFMigrationsHistory_sample VALUES ('20261018_Initial', '10.0')",
                Ct
            );
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TABLE sample_extra_items (id INTEGER PRIMARY KEY)",
                Ct
            );
        }

        _db.Insert(
            new PlayerEntity
            {
                Id = 1,
                Name = "staff-member",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
                CreatedAt = DateTime.UtcNow,
            }
        );
        _db.Insert(
            new PermissionGroupEntity
            {
                Id = 50,
                Name = "staff",
                DisplayName = "Staff",
                Weight = 10,
            }
        );

        var id = 100;

        foreach (
            var node in new[]
            {
                "sample.command.report",
                "plugin.sample.view",
                "room.enter",
                "samplex.y",
            }
        )
            _db.Insert(
                new PermissionGroupNodeEntity
                {
                    Id = id++,
                    GroupEntityId = 50,
                    Node = node,
                    Value = true,
                }
            );

        _db.Insert(
            new PlayerPermissionNodeEntity
            {
                Id = 100,
                PlayerEntityId = 1,
                Node = "sample.*",
                Value = false,
            }
        );

        await _manager.LoadAllAsync(ct: Ct);
    }

    private async Task<List<string>> TablesAsync()
    {
        await using var db = await _db.CreateDbContextAsync(Ct);

        return await db
            .Database.SqlQueryRaw<string>(
                "SELECT name AS Value FROM sqlite_master WHERE type = 'table'"
            )
            .ToListAsync(Ct);
    }

    private void WritePlugin(string key, string prefix, string? dependsOn = null)
    {
        var dir = Path.Combine(_folder, key);

        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "manifest.json"), Manifest(key, prefix, dependsOn));
        File.WriteAllText(Path.Combine(dir, "Sample.dll"), "MZ");
    }

    private static string Manifest(string key, string prefix, string? dependsOn = null) =>
        JsonSerializer.Serialize(
            new
            {
                Name = key,
                Key = key,
                Version = "1.0.0",
                Author = "Staff",
                AssemblyFile = "Sample.dll",
                TablePrefix = prefix,
                Dependencies = dependsOn is null
                    ? Array.Empty<object>()
                    : [new { Key = dependsOn }],
            }
        );

    private static byte[] Package(string key, string prefix)
    {
        using var memory = new MemoryStream();

        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (
                var (name, content) in new[]
                {
                    ("manifest.json", Manifest(key, prefix)),
                    ("Sample.dll", "MZ"),
                }
            )
            {
                using var entry = zip.CreateEntry(name).Open();
                entry.Write(Encoding.UTF8.GetBytes(content));
            }
        }

        return memory.ToArray();
    }
}
