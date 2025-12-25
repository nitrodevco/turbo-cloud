using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired.Actions;

[WiredDefinition("wf_act_move_rotate")]
public class WiredActionMoveRotateFurni(IRoomFloorItemContext ctx) : WiredAction(ctx)
{
    public override Task<bool> ExecuteAsync(IWiredContext ctx, CancellationToken ct)
    {
        return Task.FromResult(true);
    }
}
