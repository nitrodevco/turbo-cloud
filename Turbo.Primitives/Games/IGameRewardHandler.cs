using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Games;

/// <summary>
/// Hears every game result once its scores are saved, to grant rewards. A plugin declares a
/// public, concrete, top-level class implementing it; a handler that throws is logged and skipped.
/// </summary>
public interface IGameRewardHandler
{
    public Task OnGameResultAsync(GameResult result, CancellationToken ct);
}
