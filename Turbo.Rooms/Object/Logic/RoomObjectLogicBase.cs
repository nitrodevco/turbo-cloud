using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Object.Logic;

public abstract class RoomObjectLogicBase<TContext>(TContext ctx) : IRoomObjectLogic
    where TContext : IRoomObjectContext
{
    protected RoomGrain _roomGrain => (RoomGrain)_ctx.Room;
    protected readonly TContext _ctx = ctx;

    public int Id => _ctx.ObjectId.Value;

    public IRoomObjectContext Context { get; } = ctx;

    public virtual Task OnAttachAsync(CancellationToken ct) => Task.CompletedTask;

    public virtual Task OnDetachAsync(CancellationToken ct) => Task.CompletedTask;
}
