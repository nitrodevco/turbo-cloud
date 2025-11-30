using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums.Furniture;
using Turbo.Primitives.Action;

namespace Turbo.Primitives.Rooms.Object.Logic.Furniture;

public interface IFurnitureLogic : IRoomObjectLogic
{
    public double GetHeight();
    public FurniUsagePolicy GetUsagePolicy();
    public bool CanToggle();
    public Task<int> GetStateAsync();
    public Task<bool> SetStateAsync(int state);
    public Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct);
    public Task OnClickAsync(ActionContext ctx, int param, CancellationToken ct);
    public Task OnMoveAsync(ActionContext ctx, CancellationToken ct);
    public Task OnPlaceAsync(ActionContext ctx, CancellationToken ct);
    public Task OnPickupAsync(ActionContext ctx, CancellationToken ct);
}
