using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Players.Providers;

public sealed class CurrencyTypeProvider(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    ILogger<ICurrencyTypeProvider> logger
) : ICurrencyTypeProvider
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly ILogger<ICurrencyTypeProvider> _logger = logger;
    private readonly Dictionary<int, CurrencyTypeSnapshot> _currenciesById = [];
    private readonly Dictionary<CurrencyKind, int> _currencyIdsByKind = [];

    public CurrencyTypeSnapshot? GetCurrencyType(int typeId)
    {
        if (!_currenciesById.TryGetValue(typeId, out var snapshot))
            return null;

        return snapshot;
    }

    public async Task ReloadAsync(CancellationToken ct)
    {
        _currenciesById.Clear();
        _currencyIdsByKind.Clear();

        var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        try
        {
            var entities = await dbCtx
                .CurrencyTypes.AsNoTracking()
                .ToListAsync(ct)
                .ConfigureAwait(false);

            foreach (var entity in entities)
            {
                var snapshot = new CurrencyTypeSnapshot
                {
                    Id = entity.Id,
                    Name = entity.Name ?? string.Empty,
                    CurrencyType = entity.CurrencyType,
                    ActivityPointType = entity.ActivityPointType,
                    Enabled = entity.Enabled,
                };

                var kind = new CurrencyKind
                {
                    CurrencyType = snapshot.CurrencyType,
                    ActivityPointType = snapshot.ActivityPointType,
                };

                _currencyIdsByKind[kind] = snapshot.Id;
                _currenciesById[snapshot.Id] = snapshot;
            }

            _logger.LogInformation(
                "Loaded currency type mapping: Count={Count}",
                _currenciesById.Count
            );
        }
        finally
        {
            await dbCtx.DisposeAsync().ConfigureAwait(false);
        }
    }
}
