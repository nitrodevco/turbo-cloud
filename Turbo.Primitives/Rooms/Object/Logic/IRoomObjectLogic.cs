using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Rooms.Object.Logic;

public interface IRoomObjectLogic
{
    public int Id { get; }
    public IRoomObjectContext Context { get; }
    public Task OnAttachAsync(CancellationToken ct);
    public Task OnDetachAsync(CancellationToken ct);
}
