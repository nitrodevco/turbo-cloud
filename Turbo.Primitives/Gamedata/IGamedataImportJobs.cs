using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Gamedata;

/// <summary>
/// Taking in a Habbo release, in the background: the furniture asset files it needs are read first
/// (a first import reads every file Habbo has), then the definitions written
/// (<see cref="IGamedataFurnitureService.ImportAsync"/>). One at a time.
/// </summary>
public interface IGamedataImportJobs
{
    /// <summary>The import running, or the last one; null before the first.</summary>
    public GamedataImportJobSnapshot? Current { get; }

    /// <summary>
    /// Starts taking the release in. Null when there is no such release; throws
    /// <see cref="System.InvalidOperationException"/> while another import runs.
    /// </summary>
    public Task<GamedataImportJobSnapshot?> StartAsync(
        int releaseId,
        PlayerId player,
        CancellationToken ct
    );

    /// <summary>
    /// Starts taking a version of Habbo's texts in (<see cref="IGamedataTextService.ImportAsync"/>).
    /// Null when there is no such version; throws <see cref="System.InvalidOperationException"/>
    /// while another import runs.
    /// </summary>
    public Task<GamedataImportJobSnapshot?> StartTextsAsync(
        int versionId,
        PlayerId player,
        CancellationToken ct
    );

    /// <summary>
    /// Starts taking a version of Habbo's product data in (<see cref="IGamedataProductService.ImportAsync"/>).
    /// Null when there is no such version; throws <see cref="System.InvalidOperationException"/>
    /// while another import runs.
    /// </summary>
    public Task<GamedataImportJobSnapshot?> StartProductsAsync(
        int versionId,
        PlayerId player,
        CancellationToken ct
    );

    /// <summary>
    /// Starts taking a version of Habbo's figure data in (<see cref="IGamedataFigureService.ImportAsync"/>).
    /// Null when there is no such version; throws <see cref="System.InvalidOperationException"/>
    /// while another import runs.
    /// </summary>
    public Task<GamedataImportJobSnapshot?> StartFiguresAsync(
        int versionId,
        PlayerId player,
        CancellationToken ct
    );
}
