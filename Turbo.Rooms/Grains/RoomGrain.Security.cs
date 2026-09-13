using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public async Task<RoomControllerType> GetControllerLevelAsync(
        PlayerId playerId,
        CancellationToken ct
    )
    {
        return await SecurityModule.GetControllerLevelAsync(playerId);
    }

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

    public async Task GiveRightsToPlayerAsync(
        ActionContext ctx,
        PlayerId playerId,
        CancellationToken ct
    )
    {
        try
        {
            await SecurityModule.GiveRightsToPlayerAsync(ctx, playerId, ct);
        }
        catch
        {
            // TODO handle exceptions
        }
    }

    public async Task RemoveRightsFromPlayerAsync(
        ActionContext ctx,
        PlayerId playerId,
        CancellationToken ct
    )
    {
        try
        {
            await SecurityModule.RemoveRightsFromPlayerAsync(ctx, playerId, ct);
        }
        catch
        {
            // TODO handle exceptions
        }
    }
}
