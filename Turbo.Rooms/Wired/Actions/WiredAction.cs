using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired.Actions;

public abstract class WiredAction(IRoomFloorItemContext ctx) : WiredDefinition(ctx), IWiredAction
{
    public abstract Task<bool> ExecuteAsync(IWiredContext ctx, CancellationToken ct);
}
