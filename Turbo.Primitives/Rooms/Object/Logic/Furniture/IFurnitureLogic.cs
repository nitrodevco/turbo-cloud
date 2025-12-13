using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms.Object.Logic.Furniture;

public interface IFurnitureLogic : IRoomObjectLogic
{
    public double GetHeight();
    public FurnitureUsageType GetUsagePolicy();
    public bool CanToggle();
    public Task<int> GetStateAsync();
    public Task<bool> SetStateAsync(int state);
    public Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct);
    public Task OnClickAsync(ActionContext ctx, int param, CancellationToken ct);
    public Task OnMoveAsync(ActionContext ctx, CancellationToken ct);
}
