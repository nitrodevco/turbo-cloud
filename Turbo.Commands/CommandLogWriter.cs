using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;
using Turbo.Database.Entities.Room;

namespace Turbo.Commands;

/// <summary>
/// Where <see cref="OperatorCommandRunner"/> writes down a command's use: the hotel's command log
/// (<c>command_logs</c>) on a server, a list a plugin's test reads in a test hotel
/// (Turbo.Testing's <c>TestHotel.CommandLog</c>).
/// </summary>
public interface ICommandLogWriter
{
    Task WriteAsync(CommandLogEntity entry, CancellationToken ct);
}

/// <summary>The hotel's command log, one row a use.</summary>
public sealed class CommandLogWriter(IDbContextFactory<TurboDbContext> dbCtxFactory)
    : ICommandLogWriter
{
    public async Task WriteAsync(CommandLogEntity entry, CancellationToken ct)
    {
        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        dbCtx.CommandLogs.Add(entry);
        await dbCtx.SaveChangesAsync(ct);
    }
}
