using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums.Furniture;
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
    public Task<bool> SetStateAsync(int state);
    public Task OnAttachAsync(CancellationToken ct);
    public Task OnUseAsync(int param, CancellationToken ct);
    public Task OnClickAsync(int param, CancellationToken ct);
    public Task OnMoveAsync(CancellationToken ct);
    public Task OnPlaceAsync(CancellationToken ct);
    public Task OnPickupAsync(CancellationToken ct);
}
