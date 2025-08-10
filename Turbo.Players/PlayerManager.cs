using Microsoft.EntityFrameworkCore;
using Turbo.Core.Game.Players;
using Turbo.Database.Context;

namespace Turbo.Players;

public class PlayerManager(IDbContextFactory<TurboDbContext> dbContextFactory) : IPlayerManager
{
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory = dbContextFactory;
}
