using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.PacketHandlers.Moderator;

/// <summary>
/// The issue handler's close with the default sanction (<c>IssueManager.closeDefaultAction</c>):
/// the bundle is closed as resolved and the reported player gets their next default sanction.
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
            .CloseWithDefaultSanctionAsync(
                ctx.PlayerId,
                [message.IssueId, .. message.OtherIssueIds],
                ct
            )
            .ConfigureAwait(false);
}
