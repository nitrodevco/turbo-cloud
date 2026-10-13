using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.PacketHandlers.Moderator;

/// <summary>The user info window asks for a player's summary (<c>UserInfoCtrl.load</c>); an unknown player gets no answer, and the window keeps loading.</summary>
[RequiresPermission(PermissionNodes.Moderation.TOOL)]
public class GetModeratorUserInfoMessageHandler(IModerationToolService moderation)
    : IMessageHandler<GetModeratorUserInfoMessage>
{
    private readonly IModerationToolService _moderation = moderation;

    public async ValueTask HandleAsync(
        GetModeratorUserInfoMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var user = await _moderation.GetUserInfoAsync(message.UserId, ct).ConfigureAwait(false);

        if (user is null)
            return;

        await ctx.SendComposerAsync(new ModeratorUserInfoEventMessageComposer { User = user }, ct)
            .ConfigureAwait(false);
    }
}
