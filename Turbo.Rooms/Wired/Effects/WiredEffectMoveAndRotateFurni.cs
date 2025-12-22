using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired.Effects;

[WiredDefinition("wf_act_move_rotate")]
public class WiredEffectMoveAndRotateFurni(IRoomObjectContext ctx) : WiredEffect(ctx)
{
    public override Task<bool> ExecuteAsync(IWiredContext ctx, CancellationToken ct)
    {
        return Task.FromResult(true);
    }
}
