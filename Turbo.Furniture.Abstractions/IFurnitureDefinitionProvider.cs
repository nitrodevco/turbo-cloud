using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Snapshots.Furniture;

namespace Turbo.Furniture.Abstractions;

public interface IFurnitureDefinitionProvider
{
    public FurnitureDefinitionSnapshot? TryGetDefinition(int id);
    public Task ReloadAsync(CancellationToken ct = default);
}
