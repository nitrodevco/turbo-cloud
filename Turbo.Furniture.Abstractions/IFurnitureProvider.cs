using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Snapshots.Furniture;

namespace Turbo.Furniture.Abstractions;

public interface IFurnitureProvider
{
    public FurnitureSnapshot Current { get; }
    public Task ReloadAsync(CancellationToken ct = default);
}
