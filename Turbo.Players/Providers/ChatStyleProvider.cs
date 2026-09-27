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

namespace Turbo.Players.Providers;

public sealed class ChatStyleProvider(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    ILogger<IChatStyleProvider> logger
) : IChatStyleProvider
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly ILogger<IChatStyleProvider> _logger = logger;

    // Replaced whole on a reload, never cleared and refilled, so a reload that fails keeps the
    // styles the server was already running on (as CurrencyTypeProvider does).
    private FrozenDictionary<int, ChatStyleSnapshot> _stylesByClientId = FrozenDictionary<
        int,
        ChatStyleSnapshot
    >.Empty;

    public ChatStyleSnapshot? GetChatStyle(int clientStyleId) =>
        _stylesByClientId.GetValueOrDefault(clientStyleId);

    public async Task ReloadAsync(CancellationToken ct)
    {
        var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        try
        {
            var entities = await dbCtx
                .PlayerChatStyles.AsNoTracking()
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var stylesByClientId = new Dictionary<int, ChatStyleSnapshot>();

            foreach (var entity in entities)
                stylesByClientId[entity.ClientStyleId] = entity.ToSnapshot();

            _stylesByClientId = stylesByClientId.ToFrozenDictionary();

            _logger.LogInformation("Loaded chat styles: Count={Count}", _stylesByClientId.Count);
        }
        finally
        {
            await dbCtx.DisposeAsync().ConfigureAwait(false);
        }
    }
}
