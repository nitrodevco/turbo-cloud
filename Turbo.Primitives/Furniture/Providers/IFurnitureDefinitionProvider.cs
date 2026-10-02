using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Snapshots;

namespace Turbo.Primitives.Furniture.Providers;

public interface IFurnitureDefinitionProvider
{
    public FurnitureDefinitionSnapshot? TryGetDefinition(int id);

    /// <summary>Lookup by definition (class) name, case-insensitive.</summary>
    public FurnitureDefinitionSnapshot? TryGetDefinitionByName(string name);

    /// <summary>
    /// At most <paramref name="limit"/> definition names that begin with <paramref name="prefix"/>,
    /// ignoring case, sorted.
    /// </summary>
    public IReadOnlyList<string> FindNames(string prefix, int limit);
    public Task ReloadAsync(CancellationToken ct);
}
