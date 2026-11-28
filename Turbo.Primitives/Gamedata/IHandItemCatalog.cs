using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Gamedata.Snapshots;

namespace Turbo.Primitives.Gamedata;

/// <summary>The hand items described by locally stored client actions, figure assets and texts.</summary>
public interface IHandItemCatalog
{
    /// <summary>The whole catalogue: what a hand item is picked from.</summary>
    Task<HandItemCatalogSnapshot> GetAsync(CancellationToken ct);

    /// <summary>
    /// A page of the catalogue, of the hand items whose id, name or figure library holds the
    /// words; every hand item when there are none.
    /// </summary>
    Task<HandItemCatalogPage> SearchAsync(string? query, int page, CancellationToken ct);

    /// <summary>A locally rendered item frame as PNG; null when no preview can be read.</summary>
    Task<byte[]?> GetPreviewAsync(int id, CancellationToken ct);
}
