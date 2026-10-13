using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.PacketHandlers.Moderator;

/// <summary>The user info window asks for a player's chatlog (<c>UserInfoCtrl.onChatlogButton</c>).</summary>
[RequiresPermission(PermissionNodes.Moderation.TOOL)]
public class GetUserChatlogMessageHandler(IModerationToolService moderation)
    : IMessageHandler<GetUserChatlogMessage>
{
    private readonly IModerationToolService _moderation = moderation;

    public async ValueTask HandleAsync(
        GetUserChatlogMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (
            await _moderation.GetUserChatlogAsync(message.UserId, ct).ConfigureAwait(false)
            is not { } log
        )
            return;

        await ctx.SendComposerAsync(
                new UserChatlogEventMessageComposer
                {
                    UserId = message.UserId,
                    UserName = log.UserName,
                    Records = log.Records,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
