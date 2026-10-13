using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Messages.Outgoing.Callforhelp;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.PacketHandlers.Moderator;

/// <summary>
/// Which default sanction a player would get, for the issue handler's sanction text or the mod
/// action window's (<c>IssueManager.updateSanctionData</c>): the issue's reported player, or the
/// account asked about. Nothing is sent for a report about no one, or when the hotel has no default
/// sanctions.
/// </summary>
[RequiresPermission(PermissionNodes.Moderation.TOOL)]
public class ModToolSanctionMessageHandler(IModerationIssueService issues)
    : IMessageHandler<ModToolSanctionMessage>
{
    private readonly IModerationIssueService _issues = issues;

    public async ValueTask HandleAsync(
        ModToolSanctionMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        PlayerId? player =
            message.AccountId > 0
                ? message.AccountId
                : await _issues.GetReportedPlayerAsync(message.IssueId, ct).ConfigureAwait(false);

        if (player is not { } target)
            return;

        if (
            await _issues.GetDefaultSanctionAsync(target, ct).ConfigureAwait(false)
            is not { } sanction
        )
            return;

        await ctx.SendComposerAsync(
                new CfhSanctionMessageComposer
                {
                    IssueId = message.IssueId,
                    AccountId = target,
                    Sanction = sanction,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
