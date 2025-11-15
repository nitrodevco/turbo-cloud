using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Orleans.Snapshots.Navigator;

namespace Turbo.Navigator;

public sealed class NavigatorTopLevelContextProvider(
    IDbContextFactory<TurboDbContext> dbContextFactory,
    ILogger<NavigatorTopLevelContextProvider> logger
) : INavigatorTopLevelContextProvider
{
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory = dbContextFactory;
    private readonly ILogger<NavigatorTopLevelContextProvider> _logger = logger;
    private NavigatorTopLevelContextsSnapshot _current = Empty();

    public NavigatorTopLevelContextsSnapshot Current => _current;

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        var dbCtx = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        try
        {
            var entities = await dbCtx
                .NavigatorTopLevelContexts.AsNoTracking()
                .OrderBy(x => x.OrderNum)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var topLevelContexts = entities
                .Select(x => new NavigatorTopLevelContextSnapshot
                {
                    SearchCode = x.SearchCode,
                    QuickLinks = [],
                })
                .ToImmutableArray();

            var snapshot = new NavigatorTopLevelContextsSnapshot
            {
                TopLevelContexts = topLevelContexts,
            };

            _logger.LogInformation(
                "Loaded navigator snapshot: TotalTopLevelContexts={TotalTopLevelContextsCount}",
                snapshot.TopLevelContexts.Length
            );

            Volatile.Write(ref _current, snapshot);
        }
        finally
        {
            await dbCtx.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static NavigatorTopLevelContextsSnapshot Empty() =>
        new() { TopLevelContexts = ImmutableArray<NavigatorTopLevelContextSnapshot>.Empty };
}
