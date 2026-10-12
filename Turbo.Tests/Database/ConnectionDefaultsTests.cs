using FluentAssertions;
using Npgsql;
using Turbo.Database.Configuration;
using Turbo.Database.Extensions;
using Xunit;

namespace Turbo.Tests.Database;

/// <summary>
/// What the server adds to its connection string unless the string says otherwise itself.
/// Npgsql's automatic preparation: measured on the inventory and room loads and the wallet
/// update, 20 to 32% faster per query. A pool smaller than PostgreSQL allows: Npgsql's default
/// of 100 is more than a default server's 97 for ordinary roles, so a burst was refused.
/// </summary>
public sealed class ConnectionDefaultsTests
{
    private const string SERVER = "Host=db;Database=turbo;Username=turbo;Password=x";

    [Fact]
    public void The_server_prepares_its_most_used_statements_and_pools_fewer_connections_than_postgres_allows()
    {
        var built = Built(new DatabaseConfig { ConnectionString = SERVER });

        (built.MaxAutoPrepare, built.AutoPrepareMinUsages).Should().Be((50, 2));
        built.MaxPoolSize.Should().Be(50);
        (built.Host, built.Database, built.Password).Should().Be(("db", "turbo", "x"));
    }

    [Fact]
    public void Zero_leaves_each_to_npgsql()
    {
        ServiceCollectionExtensions
            .WithServerDefaults(
                new DatabaseConfig
                {
                    ConnectionString = SERVER,
                    MaxAutoPrepare = 0,
                    MaxPoolSize = 0,
                }
            )
            .Should()
            .Be(SERVER);
    }

    [Theory]
    [InlineData(";Max Auto Prepare=7", 7, 50)]
    [InlineData(";Maximum Pool Size=100", 50, 100)]
    [InlineData(";MaxPoolSize=30", 50, 30)]
    public void A_connection_string_that_sets_one_itself_wins(
        string own,
        int autoPrepare,
        int poolSize
    )
    {
        var built = Built(new DatabaseConfig { ConnectionString = SERVER + own });

        (built.MaxAutoPrepare, built.MaxPoolSize).Should().Be((autoPrepare, poolSize));
    }

    private static NpgsqlConnectionStringBuilder Built(DatabaseConfig config) =>
        new(ServiceCollectionExtensions.WithServerDefaults(config));
}
