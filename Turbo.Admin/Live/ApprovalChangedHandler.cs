using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Approvals;
using Turbo.Events.Registry;

namespace Turbo.Admin.Live;

/// <summary>
/// Tells the panel that approvals changed: the inbox, the request open and the count by its
/// name are asked again, by staff with <c>admin.approvals.view</c>.
/// </summary>
public sealed class ApprovalChangedHandler(AdminLiveFeed feed)
    : IEventHandler<ApprovalRequestedEvent>,
        IEventHandler<ApprovalDecidedEvent>,
        IEventHandler<ApprovalExecutedEvent>
{
    public ValueTask HandleAsync(
        ApprovalRequestedEvent env,
        EventContext ctx,
        CancellationToken ct
    ) => NoteAsync();

    public ValueTask HandleAsync(
        ApprovalDecidedEvent env,
        EventContext ctx,
        CancellationToken ct
    ) => NoteAsync();

    public ValueTask HandleAsync(
        ApprovalExecutedEvent env,
        EventContext ctx,
        CancellationToken ct
    ) => NoteAsync();

    private ValueTask NoteAsync()
    {
        feed.Note(approvals: true);

        return ValueTask.CompletedTask;
    }
}
