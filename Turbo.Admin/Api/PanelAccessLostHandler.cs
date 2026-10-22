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
        if (
            !env.Previous.Has(PermissionNodes.Admin.PANEL)
            || env.Current.Has(PermissionNodes.Admin.PANEL)
        )
            return;

        await grainFactory
            .GetAdminAuthGrain()
            .EndPlayerSessionsAsync(env.PlayerId, null, ct)
            .ConfigureAwait(false);
    }
}
