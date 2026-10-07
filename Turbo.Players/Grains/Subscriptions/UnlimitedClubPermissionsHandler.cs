using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Events.Registry;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Events;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Players.Grains.Subscriptions;

/// <summary>
/// A membership held by permission starts and stops with the node, not with a purchase, so the
/// client's club status, the rights it is sent and the Builders Club rooms hear of it here when
/// a <c>club.*.unlimited</c> node is granted or taken away.
/// </summary>
public sealed class UnlimitedClubPermissionsHandler(IGrainFactory grainFactory)
    : IEventHandler<PlayerPermissionsChangedEvent>
{
    public async ValueTask HandleAsync(
        PlayerPermissionsChangedEvent env,
        EventContext ctx,
        CancellationToken ct
    )
    {
        var subscriptions = grainFactory.GetPlayerSubscriptionGrain(env.PlayerId);

        if (Changed(env, PermissionNodes.Club.HABBO_CLUB_UNLIMITED))
            await subscriptions.OnChangedAsync(SubscriptionType.HabboClub, ct);

        if (Changed(env, PermissionNodes.Club.BUILDERS_CLUB_UNLIMITED))
            await subscriptions.OnChangedAsync(SubscriptionType.BuildersClub, ct);
    }

    private static bool Changed(PlayerPermissionsChangedEvent env, string node) =>
        env.Previous.Has(node) != env.Current.Has(node);
}
