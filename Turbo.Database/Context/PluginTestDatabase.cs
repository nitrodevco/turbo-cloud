using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Context;

/// <summary>
/// The database a test host (Turbo.Testing's <c>TestHotel</c>, docs/plugin-testing.md) gives
/// plugins' contexts in place of the hotel's, read from the host's services; a server registers
/// none. Their tables are made from each context's model as the plugin loads, where a server
/// runs the plugin's migrations.
/// </summary>
internal abstract class PluginTestDatabase
{
    /// <summary>Connects a plugin's context, its tables under <paramref name="tablePrefix"/>.</summary>
    public abstract void Configure(DbContextOptionsBuilder options, string tablePrefix);

    /// <summary>Makes the context's tables, as its migrations would.</summary>
    public abstract Task CreateAsync(DbContext db, CancellationToken ct);
}
