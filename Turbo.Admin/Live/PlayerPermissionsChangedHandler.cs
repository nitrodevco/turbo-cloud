using System.Threading;
using System.Threading.Tasks;
using Turbo.Events.Registry;
using Turbo.Primitives.Players.Events;

namespace Turbo.Admin.Live;

/// <summary>What a player holds changed: their permissions page is out of date.</summary>
public sealed class PlayerPermissionsChangedHandler(AdminLiveFeed feed)
    : IEventHandler<PlayerPermissionsChangedEvent>
{
    public ValueTask HandleAsync(
        PlayerPermissionsChangedEvent env,
        EventContext ctx,
        CancellationToken ct
    )
    {
        feed.Note(permissions: env.PlayerId.Value);

        return ValueTask.CompletedTask;
    }
}
