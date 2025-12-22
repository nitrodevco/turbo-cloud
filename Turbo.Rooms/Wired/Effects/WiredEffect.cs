using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired.Effects;

public abstract class WiredEffect(IRoomObjectContext ctx) : WiredDefinition(ctx), IWiredEffect
{
    public abstract Task<bool> ExecuteAsync(IWiredContext ctx, CancellationToken ct);
}
