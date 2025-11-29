using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Rooms.Object.Logic;

public interface IRoomObjectLogic
{
    public Task OnAttachAsync(CancellationToken ct);
    public Task OnDetachAsync(CancellationToken ct);
}
