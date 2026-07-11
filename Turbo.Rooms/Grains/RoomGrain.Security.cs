using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public async Task RefreshControllerLevelForPlayerAsync(ActionContext ctx, CancellationToken ct)
    {
        try
        {
            await SecurityModule.RefreshControllerLevelForPlayerAsync(ctx.PlayerId, ct);
        }
        catch
        {
            // TODO handle exceptions
        }
    }
}
