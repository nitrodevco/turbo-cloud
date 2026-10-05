using System.Threading;
using System.Threading.Tasks;
using Turbo.Events.Registry;
using Turbo.Primitives.Players.Events;

namespace Turbo.Admin.Live;

/// <summary>A player came or went: the dashboard's count, and their page, are out of date.</summary>
public sealed class PlayerOnlineChangedHandler(AdminLiveFeed feed)
    : IEventHandler<PlayerOnlineChangedEvent>
{
    public ValueTask HandleAsync(
        PlayerOnlineChangedEvent env,
        EventContext ctx,
        CancellationToken ct
    )
    {
        feed.Note(dashboard: true, player: env.PlayerId.Value);

        return ValueTask.CompletedTask;
    }
}
