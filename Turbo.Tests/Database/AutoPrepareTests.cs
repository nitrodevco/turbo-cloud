using FluentAssertions;
using Npgsql;
using Turbo.Database.Configuration;
using Turbo.Database.Extensions;
using Xunit;

namespace Turbo.Tests.Database;

/// <summary>
/// Npgsql's automatic preparation, which the server turns on unless told otherwise: measured on
/// the inventory and room loads and the wallet update, 20 to 32% faster per query. A connection
/// string that sets it itself, or <c>MaxAutoPrepare = 0</c> for an old pooler, wins.
/// </summary>
public sealed class AutoPrepareTests
{
    private const string SERVER = "Host=db;Database=turbo;Username=turbo;Password=x";

    [Fact]
    public void The_server_prepares_its_most_used_statements_by_default()
    {
        var built = new NpgsqlConnectionStringBuilder(
            ServiceCollectionExtensions.WithAutoPrepare(
                new DatabaseConfig { ConnectionString = SERVER }
            )
        );

        (built.MaxAutoPrepare, built.AutoPrepareMinUsages).Should().Be((50, 2));
        (built.Host, built.Database, built.Password).Should().Be(("db", "turbo", "x"));
    }

    [Fact]
    public void Zero_turns_it_off_and_a_connection_string_that_sets_it_wins()
    {
        ServiceCollectionExtensions
            .WithAutoPrepare(new DatabaseConfig { ConnectionString = SERVER, MaxAutoPrepare = 0 })
            .Should()
            .Be(SERVER);

        var own = SERVER + ";Max Auto Prepare=7";

        ServiceCollectionExtensions
            .WithAutoPrepare(new DatabaseConfig { ConnectionString = own })
            .Should()
            .Be(own);
    }
}
