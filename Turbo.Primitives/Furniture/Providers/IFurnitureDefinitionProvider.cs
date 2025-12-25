using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Snapshots;

namespace Turbo.Primitives.Furniture.Providers;

public interface IFurnitureDefinitionProvider
{
    public FurnitureDefinitionSnapshot? TryGetDefinition(int id);
    public Task ReloadAsync(CancellationToken ct);
}
