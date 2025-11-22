using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Rooms;

public interface IRoomModule
{
    public Task OnActivateAsync(CancellationToken ct);
    public Task OnDeactivateAsync(CancellationToken ct);
}
