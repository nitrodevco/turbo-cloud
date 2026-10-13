using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.PacketHandlers.Moderator;

/// <summary>The send-message window's message to a player (<c>SendMsgsCtrl.onSendMessageButton</c>), answered with whether it reached them.</summary>
[RequiresPermission(PermissionNodes.Moderation.TOOL)]
public class ModMessageMessageHandler(IModerationToolService moderation)
    : IMessageHandler<ModMessageMessage>
{
    private readonly IModerationToolService _moderation = moderation;

    public async ValueTask HandleAsync(
        ModMessageMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var sent = await _moderation
            .SendMessageAsync(ctx.PlayerId, message.UserId, message.Message, caution: false, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new ModeratorActionResultMessageComposer
                {
                    UserId = message.UserId,
                    Success = sent,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
