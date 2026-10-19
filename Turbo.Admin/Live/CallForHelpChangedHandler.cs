using System.Threading;
using System.Threading.Tasks;
using Turbo.Events.Registry;
using Turbo.Primitives.Moderation.Events;

namespace Turbo.Admin.Live;

/// <summary>
/// Tells the panel that calls for help changed: the reports and the bell, which lists the open
/// ones, are asked again.
/// </summary>
public sealed class CallForHelpChangedHandler(AdminLiveFeed feed)
    : IEventHandler<CallForHelpChangedEvent>
{
    public ValueTask HandleAsync(
        CallForHelpChangedEvent env,
        EventContext ctx,
        CancellationToken ct
    )
    {
        feed.Note(notifications: true, reports: true);

        return ValueTask.CompletedTask;
    }
}
