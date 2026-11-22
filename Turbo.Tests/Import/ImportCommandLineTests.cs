using Turbo.Import;
using Xunit;

namespace Turbo.Tests.Import;

/// <summary><c>Turbo.Main import</c>'s command line: what it asks for, and what it leaves to the configuration.</summary>
public class ImportCommandLineTests
{
    [Fact]
    public void A_full_command_line_reads_every_option()
    {
        var options = ImportCommandLine.Parse([
            "import",
            "morningstar",
            "--source",
            "Server=db;Database=arcturus",
            "--dry-run",
            "--rank",
            "7=admin",
            "--rank",
            "5=moderator",
            "--catalog",
            "--Turbo:Database:ConnectionString=Server=turbo",
        ]);

        Assert.Null(options.Error);
        Assert.Equal("morningstar", options.Kind);
        Assert.Equal("Server=db;Database=arcturus", options.Source);
        Assert.True(options.DryRun);
        Assert.True(options.Catalog);
        Assert.False(options.Chatlogs);
        Assert.Equal("admin", options.RankGroups[7]);
        Assert.Equal("moderator", options.RankGroups[5]);
        Assert.Equal(["--Turbo:Database:ConnectionString=Server=turbo"], options.HostArgs);
    }

    [Theory]
    [InlineData(new[] { "import" }, "Say what to import from")]
    [InlineData(new[] { "import", "plus", "--source", "x" }, "cannot import from 'plus'")]
    [InlineData(new[] { "import", "morningstar" }, "--source is required")]
    [InlineData(
        new[] { "import", "morningstar", "--source", "x", "--rank", "admin" },
        "--rank takes"
    )]
    public void A_command_line_it_cannot_run_says_why(string[] args, string error)
    {
        var options = ImportCommandLine.Parse(args);

        Assert.NotNull(options.Error);
        Assert.Contains(error, options.Error, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Help_needs_nothing_else()
    {
        var options = ImportCommandLine.Parse(["import", "--help"]);

        Assert.True(options.Help);
        Assert.Null(options.Error);
    }

    [Fact]
    public void Only_import_is_the_import_command()
    {
        Assert.True(ImportCommandLine.IsImportCommand(["IMPORT", "morningstar"]));
        Assert.False(ImportCommandLine.IsImportCommand(["migrate"]));
        Assert.False(ImportCommandLine.IsImportCommand([]));
    }
}
