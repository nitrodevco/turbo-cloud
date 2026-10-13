using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.PacketHandlers.Moderator;

/// <summary>The issue handler closes a bundle as useless (1), abusive (2) or resolved (3) (<c>IssueManager.closeBundle</c>).</summary>
[RequiresPermission(PermissionNodes.Moderation.TOOL)]
public class CloseIssuesMessageHandler(IModerationIssueService issues)
    : IMessageHandler<CloseIssuesMessage>
{
    private readonly IModerationIssueService _issues = issues;

    public async ValueTask HandleAsync(
        CloseIssuesMessage message,
        MessageContext ctx,
        CancellationToken ct
    ) =>
        await _issues
            .CloseAsync(ctx.PlayerId, message.IssueIds, (CfhCloseReasonType)message.CloseReason, ct)
            .ConfigureAwait(false);
}
