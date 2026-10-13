using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.PacketHandlers.Moderator;

/// <summary>A moderator puts issues back in the queue (<c>IssueManager.sendRelease</c>).</summary>
[RequiresPermission(PermissionNodes.Moderation.TOOL)]
public class ReleaseIssuesMessageHandler(IModerationIssueService issues)
    : IMessageHandler<ReleaseIssuesMessage>
{
    private readonly IModerationIssueService _issues = issues;

    public async ValueTask HandleAsync(
        ReleaseIssuesMessage message,
        MessageContext ctx,
        CancellationToken ct
    ) => await _issues.ReleaseAsync(ctx.PlayerId, message.IssueIds, ct).ConfigureAwait(false);
}
