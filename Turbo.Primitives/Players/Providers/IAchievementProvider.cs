using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players.Snapshots.Achievements;

namespace Turbo.Primitives.Players.Providers;

public interface IAchievementProvider
{
    public AchievementDefinitionSnapshot? GetDefinition(string code);

    public AchievementDefinitionSnapshot? GetDefinitionById(int id);

    public IReadOnlyCollection<AchievementDefinitionSnapshot> GetAllDefinitions();

    public Task ReloadAsync(CancellationToken ct);
}
