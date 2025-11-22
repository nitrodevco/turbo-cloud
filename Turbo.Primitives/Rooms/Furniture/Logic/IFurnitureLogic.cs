using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums.Furniture;
using Turbo.Contracts.Enums.Rooms.Furniture.Data;
using Turbo.Primitives.Rooms.Furniture.StuffData;

namespace Turbo.Primitives.Rooms.Furniture.Logic;

public interface IFurnitureLogic
{
    public StuffDataType StuffDataKey { get; }
    public IStuffData StuffData { get; }
    public void Setup(string stuffDataRaw);
    public double GetHeight();
    public FurniUsagePolicy GetUsagePolicy();
    public bool CanToggle();
    public bool SetState(int state);
    public Task OnAttachAsync(CancellationToken ct);
    public Task OnInteractAsync(int param, CancellationToken ct);
    public Task OnMoveAsync(CancellationToken ct);
    public Task OnPlaceAsync(CancellationToken ct);
    public Task OnPickupAsync(CancellationToken ct);
}
