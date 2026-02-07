using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Catalog.Snapshots;

namespace Turbo.Catalog.Providers;

public sealed class CatalogCurrencyTypeProvider(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    ILogger<ICatalogCurrencyTypeProvider> logger
) : ICatalogCurrencyTypeProvider
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly ILogger<ICatalogCurrencyTypeProvider> _logger = logger;
    private ImmutableDictionary<int, CatalogCurrencyTypeSnapshot> _typesById = ImmutableDictionary<
        int,
        CatalogCurrencyTypeSnapshot
    >.Empty;
    private ImmutableDictionary<string, CatalogCurrencyTypeSnapshot> _typesByKey =
        ImmutableDictionary<string, CatalogCurrencyTypeSnapshot>.Empty.WithComparers(
            StringComparer.OrdinalIgnoreCase
        );

    public async Task<CatalogCurrencyTypeSnapshot?> GetCurrencyTypeByKeyAsync(
        string currencyKey,
        CancellationToken ct
    )
    {
        if (_typesById.Count == 0)
            await ReloadAsync(ct).ConfigureAwait(false);

        _typesByKey.TryGetValue(currencyKey, out var snapshot);
        return snapshot;
    }

    public async Task<CatalogCurrencyTypeSnapshot?> GetCurrencyTypeAsync(
        int currencyTypeId,
        CancellationToken ct
    )
    {
        if (_typesById.Count == 0)
            await ReloadAsync(ct).ConfigureAwait(false);

        _typesById.TryGetValue(currencyTypeId, out var snapshot);
        return snapshot;
    }

    public async Task ReloadAsync(CancellationToken ct)
    {
        var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        try
        {
            var snapshots = await dbCtx
                .CurrencyTypes.AsNoTracking()
                .Select(x => new CatalogCurrencyTypeSnapshot
                {
                    Id = x.Id,
                    CurrencyKey = x.CurrencyKey,
                    IsActivityPoints = x.IsActivityPoints,
                    ActivityPointType = x.ActivityPointType,
                    Enabled = x.Enabled,
                })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            Volatile.Write(ref _typesById, snapshots.ToImmutableDictionary(x => x.Id));
            Volatile.Write(
                ref _typesByKey,
                snapshots.ToImmutableDictionary(
                    x => x.CurrencyKey,
                    x => x,
                    StringComparer.OrdinalIgnoreCase
                )
            );

            _logger.LogInformation(
                "Loaded catalog currency type mapping: Count={Count}",
                _typesById.Count
            );
        }
        finally
        {
            await dbCtx.DisposeAsync().ConfigureAwait(false);
        }
    }
}
