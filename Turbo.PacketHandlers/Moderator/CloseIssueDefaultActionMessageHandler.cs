using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.PacketHandlers.Moderator;

/// <summary>
/// The issue handler's close with the default sanction (<c>IssueManager.closeDefaultAction</c>):
/// the bundle is closed as resolved. The topic's default sanction is not applied here yet.
/// </summary>
[RequiresPermission(PermissionNodes.Moderation.TOOL)]
public class CloseIssueDefaultActionMessageHandler(IModerationIssueService issues)
    : IMessageHandler<CloseIssueDefaultActionMessage>
{
    private readonly IModerationIssueService _issues = issues;

    public async ValueTask HandleAsync(
        CloseIssueDefaultActionMessage message,
        MessageContext ctx,
        CancellationToken ct
    ) =>
        await _issues
            .CloseAsync(
                ctx.PlayerId,
                [message.IssueId, .. message.OtherIssueIds],
                CfhCloseReasonType.Resolved,
                ct
            )
            .ConfigureAwait(false);
}
