using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.PacketHandlers.Moderator;

/// <summary>
/// A moderator picks issues from the queue (<c>IssueManager.sendPick</c>). The ones not got come
/// back as <c>IssuePickFailed</c>, its retry count one up so that a client retrying by itself
/// (<c>ModerationMessageHandler.onIssuePickFailed</c>, under 10) stops.
/// </summary>
[RequiresPermission(PermissionNodes.Moderation.TOOL)]
public class PickIssuesMessageHandler(IModerationIssueService issues)
    : IMessageHandler<PickIssuesMessage>
{
    private readonly IModerationIssueService _issues = issues;

    public async ValueTask HandleAsync(
        PickIssuesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var failures = await _issues
            .PickAsync(ctx.PlayerId, message.IssueIds, ct)
            .ConfigureAwait(false);

        if (failures.IsEmpty)
            return;

        await ctx.SendComposerAsync(
                new IssuePickFailedMessageComposer
                {
                    Issues = failures,
                    RetryEnabled = message.RetryEnabled,
                    RetryCount = message.RetryCount + 1,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
