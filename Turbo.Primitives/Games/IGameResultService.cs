using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Games;

/// <summary>Where a game reports how it ended: stores the scores, then calls the reward handlers.</summary>
public interface IGameResultService
{
    public Task ReportAsync(GameResult result, CancellationToken ct);
}
