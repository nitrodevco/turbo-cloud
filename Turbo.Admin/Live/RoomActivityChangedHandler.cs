using System.Threading;
using System.Threading.Tasks;
using Turbo.Events.Registry;
using Turbo.Primitives.Rooms.Events;

namespace Turbo.Admin.Live;

/// <summary>
/// A room loaded, unloaded, changed, or someone entered or left it: the dashboard's rooms, the
/// room's page, and the page of whoever moved are out of date.
/// </summary>
public sealed class RoomActivityChangedHandler(AdminLiveFeed feed)
    : IEventHandler<RoomActivityChangedEvent>
{
    public ValueTask HandleAsync(
        RoomActivityChangedEvent env,
        EventContext ctx,
        CancellationToken ct
    )
    {
        feed.Note(dashboard: true, room: env.RoomId.Value, player: env.PlayerId?.Value);

        return ValueTask.CompletedTask;
    }
}
