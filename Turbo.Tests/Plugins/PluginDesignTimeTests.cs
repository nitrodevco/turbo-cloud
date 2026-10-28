using System.IO;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Turbo.Database.Context;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// <c>dotnet ef migrations add</c> in a plugin's project builds its context with no factory of
/// the plugin's: the table prefix comes from the plugin's manifest.json, above the build folder,
/// so the migrations name the tables (and the history table) the plugin uses once a hotel loads
/// it.
/// </summary>
public sealed class PluginDesignTimeTests : IDisposable
{
    private readonly string _plugin = Path.Combine(
        Path.GetTempPath(),
        "turbo-plugin-design-" + Guid.NewGuid().ToString("N")
    );

    public void Dispose()
    {
        if (Directory.Exists(_plugin))
            Directory.Delete(_plugin, recursive: true);
    }

    [Fact]
    public void Design_time_takes_the_prefix_of_the_manifest_above_the_build_folder_and_the_connection_given()
    {
        var build = Path.Combine(_plugin, "server", "bin", "Debug", "net10.0");

        Directory.CreateDirectory(build);
        File.WriteAllText(
            Path.Combine(_plugin, "manifest.json"),
            """{ "Key": "skills", "TablePrefix": "skills_" }"""
        );

        var design = PluginDesignTime.Read(
            [null, build],
            ["ef", "migrations", "add", "Init", "--connection", "server=db;database=hotel"],
            variable => variable == PluginDesignTime.CONNECTION_VARIABLE ? "server=other" : null
        );
        var options = new DbContextOptionsBuilder();

        design.Configure(options);

        design.TablePrefix.Should().Be("skills_");
        design.Manifest.Should().Be(Path.Combine(_plugin, "manifest.json"));

        var mysql = RelationalOptionsExtension.Extract(options.Options);

        mysql.ConnectionString.Should().Be("server=db;database=hotel", "the argument wins");
        mysql.MigrationsHistoryTableName.Should().Be("__EFMigrationsHistory_skills");
    }
}
