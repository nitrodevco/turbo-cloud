using Microsoft.Extensions.Options;
using Turbo.Database.Backups;
using Turbo.Database.Configuration;

namespace Turbo.Tests.Support;

/// <summary>The SQL writer as the host registers it, with the database's default settings.</summary>
internal static class TestDatabaseDump
{
    public static DatabaseDump Create(DatabaseConfig? config = null) =>
        new(Options.Create(config ?? new DatabaseConfig()), TimeProvider.System);
}
