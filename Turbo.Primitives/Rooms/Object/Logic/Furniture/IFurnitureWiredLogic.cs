using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots.WiredData;
using Turbo.Primitives.Furniture.WiredData;

namespace Turbo.Primitives.Rooms.Object.Logic.Furniture;

public interface IFurnitureWiredLogic : IFurnitureFloorLogic
{
    public WiredType WiredType { get; }
    public int WiredCode { get; }
    public IWiredData WiredData { get; }
    public Task ConfigureWiredAsync(CancellationToken ct);
    public WiredDataSnapshot GetSnapshot();
}
