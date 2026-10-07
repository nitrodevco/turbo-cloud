using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Gamedata.Snapshots;

namespace Turbo.Primitives.Gamedata;

/// <summary>
/// Habbo's releases: its external variables asked which revision it serves, and the furniture
/// data it serves kept, so an import works from what was checked rather than asking again.
/// </summary>
public interface IHabboReleaseService
{
    /// <summary>
    /// Asks Habbo what it serves now and keeps it when it is new. Throws
    /// <see cref="System.Net.Http.HttpRequestException"/> when Habbo can't be reached or answers
    /// with something that isn't its gamedata.
    /// </summary>
    public Task<HabboCheckResult> CheckAsync(CancellationToken ct);

    /// <summary>The newest release found; null before the first check.</summary>
    public Task<HabboReleaseSnapshot?> GetLatestAsync(CancellationToken ct);

    /// <summary>The newest version of Habbo's external texts found; null before the first check.</summary>
    public Task<HabboTextVersionSnapshot?> GetLatestTextsAsync(CancellationToken ct);

    /// <summary>The newest version of Habbo's product data found; null before the first check.</summary>
    public Task<HabboProductVersionSnapshot?> GetLatestProductsAsync(CancellationToken ct);

    /// <summary>The newest version of Habbo's figure data found; null before the first check.</summary>
    public Task<HabboFigureVersionSnapshot?> GetLatestFiguresAsync(CancellationToken ct);

    /// <summary>A release by its id; null when there is none.</summary>
    public Task<HabboReleaseSnapshot?> GetAsync(int id, CancellationToken ct);
}
