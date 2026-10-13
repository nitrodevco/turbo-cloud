using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.PacketHandlers.Moderator;

/// <summary>The issue handler asks for the chat a call for help was sent with (<c>IssueHandler.show</c>).</summary>
[RequiresPermission(PermissionNodes.Moderation.TOOL)]
public class GetCfhChatlogMessageHandler(IModerationIssueService issues)
    : IMessageHandler<GetCfhChatlogMessage>
{
    private readonly IModerationIssueService _issues = issues;

    public async ValueTask HandleAsync(
        GetCfhChatlogMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (await _issues.GetChatlogAsync(message.IssueId, ct).ConfigureAwait(false) is not { } log)
            return;

        await ctx.SendComposerAsync(
                new CfhChatlogEventMessageComposer
                {
                    CallId = message.IssueId,
                    CallerUserId = log.ReporterId,
                    ReportedUserId = log.ReportedId,
                    ChatRecordId = message.IssueId,
                    Record = log.Record,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
