namespace Turbo.Players;

using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using Orleans;

using Turbo.Core.Contracts.Players;
using Turbo.Core.Game.Players;
using Turbo.Database.Context;

public class PlayerManager(
    IDbContextFactory<TurboDbContext> dbContextFactory,
    IGrainFactory grainFactory) : IPlayerManager
{
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory = dbContextFactory;
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async Task<IPlayerGrain> GetPlayerGrain(long playerId)
    {
        if (playerId <= 0)
        {
            return null;
        }

        var grain = _grainFactory.GetGrain<IPlayerGrain>(playerId);

        return await Task.FromResult(grain);
    }

    public async Task<bool> PlayerExistsAsync(long playerId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();

        return await db.Players.AsNoTracking().AnyAsync(p => p.Id == playerId);
    }
}
