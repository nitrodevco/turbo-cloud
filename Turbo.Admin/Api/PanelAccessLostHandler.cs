using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Events.Registry;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Events;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// A player who no longer holds <c>admin.panel</c> is signed out of the panel everywhere, so
/// giving it back later does not bring back sessions opened before. The session filter does the
/// same for a loss this event did not carry (a group's node taken away while they were offline).
/// A preview of a group without the node is not a loss: only the player's own permissions count,
/// which are what the event carries once no preview runs.
/// </summary>
public sealed class PanelAccessLostHandler(IGrainFactory grainFactory)
    : IEventHandler<PlayerPermissionsChangedEvent>
{
    public async ValueTask HandleAsync(
        PlayerPermissionsChangedEvent env,
        EventContext ctx,
        CancellationToken ct
    )
    {
        // Previous was a preview when this is its end: they held the node to start one.
        if (
            env.Current.Preview is not null
            || env.Current.Has(PermissionNodes.Admin.PANEL)
            || (env.Previous.Preview is null && !env.Previous.Has(PermissionNodes.Admin.PANEL))
        )
            return;

        await grainFactory
            .GetAdminAuthGrain()
            .EndPlayerSessionsAsync(env.PlayerId, null, ct)
            .ConfigureAwait(false);
    }
}
