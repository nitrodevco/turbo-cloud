using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Rooms.Object.Logic;

public interface IRoomObjectLogic<out TObject, out TSelf, out TContext> : IRoomObjectLogic
    where TObject : IRoomObject<TObject, TSelf, TContext>
    where TContext : IRoomObjectContext<TObject, TSelf, TContext>
    where TSelf : IRoomObjectLogic<TObject, TSelf, TContext>
{
    new TContext Context { get; }
}

public interface IRoomObjectLogic
{
    public IRoomObjectContext Context { get; }
    public Task OnAttachAsync(CancellationToken ct);
    public Task OnDetachAsync(CancellationToken ct);
}
