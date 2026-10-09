using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;
using Turbo.Database.Extensions;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Moderation.Snapshots;

namespace Turbo.Operations;

/// <summary>
/// Calls for help. The topics are <c>cfh_topics</c>, read on first use and held: every login
/// sends them, and a hotel changes them about never (a change shows after a restart).
/// </summary>
public sealed class CallForHelpService(IDbContextFactory<TurboDbContext> dbCtxFactory)
    : ICallForHelpService
{
    private readonly SemaphoreSlim _topicsLock = new(1, 1);
    private ImmutableArray<CfhCategorySnapshot>? _topics;

    public async Task<ImmutableArray<CfhCategorySnapshot>> GetTopicsAsync(CancellationToken ct)
    {
        if (_topics is { } held)
            return held;

        await _topicsLock.WaitAsync(ct);

        try
        {
            if (_topics is { } read)
                return read;

            await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

            var rows = await dbCtx
                .CfhTopics.AsNoTracking()
                .Where(x => x.Enabled)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .ToListAsync(ct);

            // GroupBy keeps the order each category is first met in.
            var topics = rows.GroupBy(x => x.Category)
                .Select(g => new CfhCategorySnapshot
                {
                    Name = g.Key,
                    Topics = [.. g.Select(x => x.ToSnapshot())],
                })
                .ToImmutableArray();

            _topics = topics;

            return topics;
        }
        finally
        {
            _topicsLock.Release();
        }
    }
}
