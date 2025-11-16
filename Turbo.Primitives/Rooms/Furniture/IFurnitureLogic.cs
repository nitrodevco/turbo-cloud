using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums.Furniture;
using Turbo.Contracts.Enums.Rooms.Furniture.Data;
using Turbo.Primitives.Rooms.StuffData;

namespace Turbo.Primitives.Rooms.Furniture;

public interface IFurnitureLogic
{
    public StuffDataTypeEnum StuffDataKey { get; }
    public ValueTask OnInteractAsync(FurnitureContext ctx, CancellationToken ct);
    public ValueTask OnMoveAsync(FurnitureContext ctx, CancellationToken ct);
    public ValueTask OnPlaceAsync(FurnitureContext ctx, CancellationToken ct);
    public ValueTask OnPickupAsync(FurnitureContext ctx, CancellationToken ct);
    public bool CanToggle(FurnitureContext ctx);
    public FurniUsagePolicy GetUsagePolicy(FurnitureContext ctx);
    public IStuffData CreateStuffData();
    public IStuffData CreateStuffDataFromJson(string json);
}
