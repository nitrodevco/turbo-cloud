using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Turbo.Database.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Extensions;
using Turbo.Database.Migrations;
using Xunit;

namespace Turbo.Tests.Database;

/// <summary>
/// The emulator's own migrations and how they are wired, with no database server: the model has
/// not drifted from its migrations (a changed entity with no migration is the "Unknown column"
/// that stops every login), the SQL for a database can be written offline, and the migrator is
/// built from the configuration.
/// </summary>
public sealed class CoreMigrationsTests
{
    // Nothing listens on port 1: if anything tries to connect, the test fails at once.
    private const string UNREACHABLE = "server=127.0.0.1;port=1;user=x;password=x;database=turbo";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static TurboDbContext OfflineContext() =>
        new(
            new DbContextOptionsBuilder<TurboDbContext>()
                .UseMySql(
                    UNREACHABLE,
                    new MySqlServerVersion(new Version(8, 4, 0)),
                    mysql => mysql.MigrationsAssembly("Turbo.Database")
                )
                .Options
        );

    [Fact]
    public void TheModelHasNoChangesThatLackAMigration()
    {
        using var db = OfflineContext();

        db.Database.HasPendingModelChanges()
            .Should()
            .BeFalse(
                "an entity or mapping was changed without 'dotnet ef migrations add': the database "
                    + "would not have the column or table the code reads"
            );
    }

    [Fact]
    public void TheScriptForAnEmptyDatabaseNeedsNoConnection_AndNamesEveryMigration()
    {
        using var db = OfflineContext();

        var script = DatabaseMigrator.GenerateScript(db);

        foreach (var id in db.GetService<IMigrationsAssembly>().Migrations.Keys)
            script.Should().Contain(id);

        script.Should().Contain("CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory`");
        script.Should().Contain("IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory`");
    }

    [Fact]
    public void EveryMigrationHasAnIdThatSortsInTheOrderItWasMade()
    {
        using var db = OfflineContext();

        var ids = db.GetService<IMigrationsAssembly>().Migrations.Keys.ToList();

        ids.Should().OnlyHaveUniqueItems();
        ids.All(x => x.Length > 15 && x.Take(14).All(char.IsAsciiDigit) && x[14] == '_')
            .Should()
            .BeTrue();
    }

    [Fact]
    public void TheMigratorIsBuiltFromTheConfiguration_AndNeedsNoServerToStartWhenTheVersionIsGiven()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Turbo:Database:ConnectionString"] = UNREACHABLE,
                ["Turbo:Database:ServerVersion"] = "mysql 8.4.0",
                ["Turbo:Database:Migrate"] = "check",
                ["Turbo:Database:AllowDestructiveMigrations"] = "true",
                ["Turbo:Database:MigrationLockSeconds"] = "5",
            }
        );
        builder.Services.AddTurboDatabaseContext(builder);
        using var provider = builder.Services.BuildServiceProvider();

        var config = provider.GetRequiredService<IOptions<DatabaseConfig>>().Value;
        provider.GetRequiredService<DatabaseMigrator>().Should().NotBeNull();
        provider.GetRequiredService<IMigrationLock>().Should().BeOfType<MySqlMigrationLock>();

        config.Migrate.Should().Be(MigrationMode.Check);
        config.AllowDestructiveMigrations.Should().BeTrue();
        config.MigrationLockSeconds.Should().Be(5);

        // A context from the factory is made without asking the server what version it is.
        using var db = provider
            .GetRequiredService<IDbContextFactory<TurboDbContext>>()
            .CreateDbContext();

        DatabaseMigrator.GenerateScript(db).Should().Contain("CREATE TABLE");
    }

    [Fact]
    public void TheDefaultsAreTheSafeOnesForAHotelThatSetsNothing()
    {
        var config = new DatabaseConfig();

        config.Migrate.Should().Be(MigrationMode.Auto);
        config.AllowDestructiveMigrations.Should().BeFalse();
        config.MigrationLockSeconds.Should().Be(60);
        config.MigrationCommandTimeoutMinutes.Should().Be(30);
        config.ServerVersion.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Auto", MigrationMode.Auto)]
    [InlineData("check", MigrationMode.Check)]
    [InlineData("OFF", MigrationMode.Off)]
    public void TheModeIsReadFromTextInAnyCase(string text, MigrationMode expected)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Migrate"] = text })
            .Build()
            .Get<DatabaseConfig>()!;

        config.Migrate.Should().Be(expected);
    }

    [Fact]
    public void AppSettingsShipTheDefaultsItDocuments()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "appsettings.json"
        );
        var config = new ConfigurationBuilder()
            .AddJsonFile(path)
            .Build()
            .GetSection(DatabaseConfig.SECTION_NAME)
            .Get<DatabaseConfig>()!;

        config.Migrate.Should().Be(MigrationMode.Auto);
        config.AllowDestructiveMigrations.Should().BeFalse();
    }

    [Theory]
    [InlineData("db", "turbo:migrate:db")]
    [InlineData("", "turbo:migrate:")]
    [InlineData(null, "turbo:migrate:")]
    public void TheLockIsNamedAfterTheDatabase(string? database, string expected) =>
        MySqlMigrationLock.NameFor(database).Should().Be(expected);

    [Fact]
    public void ALongDatabaseNameStillMakesAUsableLockName_OneEachAndTheSameEveryTime()
    {
        var a = MySqlMigrationLock.NameFor(new string('a', 64));
        var b = MySqlMigrationLock.NameFor(new string('a', 63) + "b");

        a.Length.Should().BeLessThanOrEqualTo(64);
        b.Length.Should().BeLessThanOrEqualTo(64);
        a.Should().NotBe(b);
        a.Should().Be(MySqlMigrationLock.NameFor(new string('a', 64)));
    }

    [Fact]
    public async Task AStateReadFromNoDatabaseAtAllIsNotInventedHere()
    {
        // The state of a database is only ever read from the database: no connection, no state.
        using var db = OfflineContext();
        var migrator = new DatabaseMigrator(
            Options.Create(new DatabaseConfig()),
            new MySqlMigrationLock(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DatabaseMigrator>.Instance
        );

        var act = () => migrator.InspectAsync(db, Ct);

        await act.Should().ThrowAsync<Exception>();
    }
}
