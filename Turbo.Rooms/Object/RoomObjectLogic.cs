using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Object;

public abstract class RoomObjectLogic<TObject, TSelf, TContext>(TContext ctx)
    : IRoomObjectLogic<TObject, TSelf, TContext>
    where TObject : IRoomObject<TObject, TSelf, TContext>
    where TContext : IRoomObjectContext<TObject, TSelf, TContext>
    where TSelf : IRoomObjectLogic<TObject, TSelf, TContext>
{
    protected readonly TContext _ctx = ctx;
    protected readonly RoomGrain _roomGrain = (RoomGrain)ctx.Room;

    public TContext Context => _ctx;

    IRoomObjectContext IRoomObjectLogic.Context => Context;

    public virtual Task OnAttachAsync(CancellationToken ct) => Task.CompletedTask;

    public virtual Task OnDetachAsync(CancellationToken ct) => Task.CompletedTask;
}
