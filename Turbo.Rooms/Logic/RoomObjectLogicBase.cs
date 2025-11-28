using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Rooms.Logic;

public abstract class RoomObjectLogicBase<TContext>(TContext ctx)
    where TContext : IRoomObjectContext
{
    protected readonly TContext _ctx = ctx;

    public virtual Task OnAttachAsync(CancellationToken ct) => Task.CompletedTask;
}
