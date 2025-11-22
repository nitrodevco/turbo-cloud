using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums.Furniture;
using Turbo.Contracts.Enums.Rooms.Furniture.Data;

namespace Turbo.Primitives.Rooms.Furniture.Logic;

public interface IFurnitureLogic
{
    public StuffDataType StuffDataKey { get; }
    public bool CanToggle();
    public double GetHeight();
    public FurniUsagePolicy GetUsagePolicy();
    public Task SetStateAsync(int state, CancellationToken ct);
    public void SetupStuffDataFromJson(string json);
    public Task OnInteractAsync(int param, CancellationToken ct);
    public Task OnMoveAsync(CancellationToken ct);
    public Task OnPlaceAsync(CancellationToken ct);
    public Task OnPickupAsync(CancellationToken ct);
}
