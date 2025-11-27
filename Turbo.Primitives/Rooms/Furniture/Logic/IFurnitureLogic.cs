using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums.Furniture;
using Turbo.Primitives.Actor;

namespace Turbo.Primitives.Rooms.Furniture.Logic;

public interface IFurnitureLogic
{
    public double GetHeight();
    public FurniUsagePolicy GetUsagePolicy();
    public bool CanToggle();
    public Task<bool> SetStateAsync(int state);
    public Task OnAttachAsync(CancellationToken ct);
    public Task OnUseAsync(ActorContext ctx, int param, CancellationToken ct);
    public Task OnClickAsync(ActorContext ctx, int param, CancellationToken ct);
    public Task OnMoveAsync(ActorContext ctx, CancellationToken ct);
    public Task OnPlaceAsync(ActorContext ctx, CancellationToken ct);
    public Task OnPickupAsync(ActorContext ctx, CancellationToken ct);
}
