using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Gamedata.Snapshots;

namespace Turbo.Primitives.Gamedata;

/// <summary>
/// The gamedata files the client loads, built from the database (<see cref="GamedataFiles"/>):
/// FurnitureData from the furniture definitions, each item's offer ids stamped from the catalogs
/// as players see them, and the external texts. A file is
/// addressed by the hash of its content, as Habbo's are, so it can be cached for good; the
/// builds before the current one are kept for clients still holding their address.
/// </summary>
public interface IGamedataFileService
{
    /// <summary>
    /// The file as the database has it now, built again when what it is made from has changed
    /// (a catalog published, definitions changed through <see cref="Invalidate"/>).
    /// </summary>
    public Task<GamedataFileContent> GetCurrentAsync(string file, CancellationToken ct);

    /// <summary>A build of the file by its hash, current or kept; null when there is none.</summary>
    public Task<GamedataFileContent?> GetAsync(string file, string hash, CancellationToken ct);

    /// <summary>Builds the file again whether or not anything is known to have changed.</summary>
    public Task<GamedataFileContent> RebuildAsync(string file, CancellationToken ct);

    /// <summary>Says what the file is made from changed, so the next request builds it again.</summary>
    public void Invalidate(string file);
}
