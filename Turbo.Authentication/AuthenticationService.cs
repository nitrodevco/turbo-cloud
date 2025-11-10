using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Database.Context;
using Turbo.Primitives.Authentication;
using Turbo.Primitives.Grains;
using Turbo.Primitives.Grains.Observers;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans.Snapshots.Session;

namespace Turbo.Authentication;

public sealed class AuthenticationService(
    IDbContextFactory<TurboDbContext> dbContextFactory,
    IGrainFactory grainFactory,
    ISessionGateway sessionGateway
) : IAuthenticationService
{
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory = dbContextFactory;
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly ISessionGateway _sessionGateway = sessionGateway;

    public async Task<int> GetPlayerIdFromTicketAsync(string ticket, CancellationToken ct = default)
    {
        if (ticket is null || ticket.Length == 0)
            return 0;

        var dbCtx = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        try
        {
            if (dbCtx.SecurityTickets is null)
                return 0;

            var entity = await dbCtx
                .SecurityTickets.AsNoTracking()
                .FirstOrDefaultAsync(entity => entity.Ticket == ticket, ct)
                .ConfigureAwait(false);

            if (entity is null)
                return 0;

            // check timestamp for expiration, if time now is greater than expiration, return 0;

            if (!entity.IsLocked)
            {
                dbCtx.SecurityTickets.Remove(entity);

                await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            return entity.PlayerEntityId;
        }
        finally
        {
            await dbCtx.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async Task AssociateSessionWithPlayerAsync(SessionKey key, long playerId)
    {
        _sessionGateway.BindSessionToPlayer(key, playerId);

        var observer = _grainFactory.CreateObjectReference<ISessionContextObserver>(
            new SessionContextObserver(key, _sessionGateway)
        );
        var playerPresence = _grainFactory.GetGrain<IPlayerPresenceGrain>(playerId);

        await playerPresence.RegisterAsync(key, observer).ConfigureAwait(false);

        // on session d.c

        var dc = false;

        if (dc)
        {
            await playerPresence.UnregisterAsync(key).ConfigureAwait(false);
            _sessionGateway.UnbindSessionFromPlayer(key);
            _grainFactory.DeleteObjectReference<ISessionContextObserver>(observer);
        }
    }
}
