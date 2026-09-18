using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Snapshots;

namespace Turbo.Primitives.Furniture.Providers;

public interface IFurnitureDefinitionProvider
{
    public FurnitureDefinitionSnapshot? TryGetDefinition(int id);

    /// <summary>Lookup by definition (class) name, case-insensitive.</summary>
    public FurnitureDefinitionSnapshot? TryGetDefinitionByName(string name);
    public Task ReloadAsync(CancellationToken ct);
}
