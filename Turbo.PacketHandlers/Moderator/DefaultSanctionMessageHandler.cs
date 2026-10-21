using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.PacketHandlers.Moderator;

/// <summary>The mod action window's "Default sanction" (<c>ModActionCtrl.as:214-231</c>): the player's next default sanction, the one the window's label showed (<c>IssueManager.updateSanctionData</c>). Answered with whether it was given, as the window's other actions are.</summary>
[RequiresPermission(PermissionNodes.Moderation.TOOL)]
public class DefaultSanctionMessageHandler(IModerationIssueService issues)
    : IMessageHandler<DefaultSanctionMessage>
{
    private readonly IModerationIssueService _issues = issues;

    public async ValueTask HandleAsync(
        DefaultSanctionMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var given = await _issues
            .GiveDefaultSanctionAsync(
                ctx.PlayerId,
                message.UserId,
                message.TopicId,
                message.Message,
                message.IssueId,
                ct
            )
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new ModeratorActionResultMessageComposer
                {
                    UserId = message.UserId,
                    Success = given,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
