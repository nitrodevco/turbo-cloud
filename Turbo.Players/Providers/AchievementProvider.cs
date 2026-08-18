using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots.Achievements;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Players.Providers;

public sealed class AchievementProvider(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    ILogger<IAchievementProvider> logger
) : IAchievementProvider
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly ILogger<IAchievementProvider> _logger = logger;
    private readonly Dictionary<string, AchievementDefinitionSnapshot> _definitionsByCode = [];
    private readonly Dictionary<int, AchievementDefinitionSnapshot> _definitionsById = [];

    public AchievementDefinitionSnapshot? GetDefinition(string code)
    {
        if (string.IsNullOrEmpty(code))
            return null;

        return _definitionsByCode.TryGetValue(code, out var snapshot) ? snapshot : null;
    }

    public AchievementDefinitionSnapshot? GetDefinitionById(int id) =>
        _definitionsById.TryGetValue(id, out var snapshot) ? snapshot : null;

    public IReadOnlyCollection<AchievementDefinitionSnapshot> GetAllDefinitions() =>
        _definitionsByCode.Values;

    public async Task ReloadAsync(CancellationToken ct)
    {
        _definitionsByCode.Clear();
        _definitionsById.Clear();

        var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        try
        {
            var entities = await dbCtx
                .Achievements.AsNoTracking()
                .Include(x => x.Levels)
                .ThenInclude(x => x.CurrencyTypeEntity)
                .Where(x => x.Enabled)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            foreach (var entity in entities)
            {
                var levels = (entity.Levels ?? [])
                    .OrderBy(x => x.Level)
                    .Select(x => new AchievementLevelSnapshot
                    {
                        Level = x.Level,
                        GoalCount = x.GoalCount,
                        ScoreReward = x.ScoreReward,
                        CurrencyKind = x.CurrencyTypeEntity is null
                            ? null
                            : new CurrencyKind
                            {
                                CurrencyType = x.CurrencyTypeEntity.CurrencyType,
                                ActivityPointType = x.CurrencyTypeEntity.ActivityPointType,
                            },
                        CurrencyReward = x.CurrencyReward,
                        BadgeCode = x.BadgeCode,
                    })
                    .ToList();

                var snapshot = new AchievementDefinitionSnapshot
                {
                    Id = entity.Id,
                    Code = entity.Code,
                    Name = entity.Name,
                    Category = entity.Category,
                    MaxLevel = levels.Count > 0 ? levels[^1].Level : 0,
                    Levels = levels,
                };

                _definitionsByCode[snapshot.Code] = snapshot;
                _definitionsById[snapshot.Id] = snapshot;
            }

            _logger.LogInformation(
                "Loaded achievement definitions: Count={Count}",
                _definitionsByCode.Count
            );
        }
        finally
        {
            await dbCtx.DisposeAsync().ConfigureAwait(false);
        }
    }
}
