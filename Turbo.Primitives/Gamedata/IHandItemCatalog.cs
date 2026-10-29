using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Gamedata.Snapshots;

namespace Turbo.Primitives.Gamedata;

/// <summary>The hand items described by locally stored client actions, figure assets and texts.</summary>
public interface IHandItemCatalog
{
    Task<HandItemCatalogSnapshot> GetAsync(CancellationToken ct);

    /// <summary>A locally rendered item frame as PNG; null when no preview can be read.</summary>
    Task<byte[]?> GetPreviewAsync(int id, CancellationToken ct);
}
