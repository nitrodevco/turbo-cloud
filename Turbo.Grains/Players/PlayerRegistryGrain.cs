using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Contracts.Players;
using Turbo.Contracts.Shared;
using Turbo.Database.Context;

namespace Turbo.Grains.Players;

public sealed class PlayerRegistryGrain : Grain, IPlayerRegistryGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory;

    public PlayerRegistryGrain(IDbContextFactory<TurboDbContext> dbContextFactory) => _dbContextFactory = dbContextFactory;

    public async Task<bool> ExistsAsync()
    {
        var id = this.GetPrimaryKeyString();

        await using var db = await _dbContextFactory.CreateDbContextAsync();

        return await db.Players.AsNoTracking().AnyAsync(p => p.Id.ToString() == id);
    }

    public async Task<EnsureResult<PlayerSummary>> EnsureExistsAsync(bool createIfMissing = false)
    {
        var id = this.GetPrimaryKeyString();

        await using var db = await _dbContextFactory.CreateDbContextAsync();

        var entity = await db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.Id.ToString() == id);

        if (entity is not null)
        {
            return new EnsureResult<PlayerSummary>(
                EnsureStatus.Ok,
                new PlayerSummary(entity.Id.ToString(), entity.Name, entity.Motto, entity.Figure));
        }

        if (!createIfMissing)
            return new EnsureResult<PlayerSummary>(EnsureStatus.NotFound, null);

        // If createIfMissing is true and entity is not found, return NotFound or handle creation logic here
        return new EnsureResult<PlayerSummary>(EnsureStatus.NotFound, null);
    }
}