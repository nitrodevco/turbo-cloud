using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Database.Context;

namespace Turbo.Authentication;

public sealed class AuthenticationService(IServiceScopeFactory serviceScopeFactory)
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;

    public async Task<int> GetPlayerIdFromTicketAsync(string ticket)
    {
        if (ticket is null || ticket.Length == 0)
            return 0;

        using var scope = _serviceScopeFactory.CreateScope();

        var dbCtx = scope.ServiceProvider.GetRequiredService<TurboDbContext>();

        if (dbCtx.SecurityTickets is null)
            return 0;

        var entity = await dbCtx
            .SecurityTickets.FirstOrDefaultAsync(entity => entity.Ticket == ticket)
            .ConfigureAwait(false);

        if (entity is null)
            return 0;

        // check timestamp for expiration, if time now is greater than expiration, return 0;

        if (!entity.IsLocked)
        {
            dbCtx.SecurityTickets.Remove(entity);

            await dbCtx.SaveChangesAsync().ConfigureAwait(false);
        }

        return entity.PlayerEntityId;
    }
}
