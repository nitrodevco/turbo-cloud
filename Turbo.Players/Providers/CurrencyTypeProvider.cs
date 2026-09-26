using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Database.Extensions;
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

    // Replaced whole on a reload, never cleared and refilled, so a reload that fails keeps the
    // mapping the server was already running on (as FurnitureDefinitionProvider does).
    private FrozenDictionary<int, CurrencyTypeSnapshot> _currenciesById = FrozenDictionary<
        int,
        CurrencyTypeSnapshot
    >.Empty;
    private FrozenDictionary<CurrencyKind, int> _currencyIdsByKind = FrozenDictionary<
        CurrencyKind,
        int
    >.Empty;

    public CurrencyTypeSnapshot? GetCurrencyType(int typeId)
    {
        if (!_currenciesById.TryGetValue(typeId, out var snapshot))
            return null;

        return snapshot;
    }

    public bool TryGetCurrencyTypeId(CurrencyKind kind, out int typeId) =>
        _currencyIdsByKind.TryGetValue(kind, out typeId);

    public async Task ReloadAsync(CancellationToken ct)
    {
        var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        try
        {
            var entities = await dbCtx
                .CurrencyTypes.AsNoTracking()
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var currenciesById = new Dictionary<int, CurrencyTypeSnapshot>();
            var currencyIdsByKind = new Dictionary<CurrencyKind, int>();

            foreach (var entity in entities)
            {
                var snapshot = entity.ToSnapshot();

                var kind = new CurrencyKind
                {
                    CurrencyType = snapshot.CurrencyType,
                    ActivityPointType = snapshot.ActivityPointType,
                };

                currencyIdsByKind[kind] = snapshot.Id;
                currenciesById[snapshot.Id] = snapshot;
            }

            _currenciesById = currenciesById.ToFrozenDictionary();
            _currencyIdsByKind = currencyIdsByKind.ToFrozenDictionary();

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
