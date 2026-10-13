using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.PacketHandlers.Moderator;

/// <summary>The mod action window's caution (<c>ModActionCtrl.onCustomSanctionButton</c>, "Alert"), answered with whether it reached the player. The tool offers it only to holders of <c>command.warn</c>, which the server holds it to as well.</summary>
[RequiresPermission(PermissionNodes.Moderation.TOOL)]
public class ModAlertMessageHandler(IModerationToolService moderation)
    : IMessageHandler<ModAlertMessage>
{
    private readonly IModerationToolService _moderation = moderation;

    public async ValueTask HandleAsync(
        ModAlertMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var sent = await _moderation
            .SendMessageAsync(ctx.PlayerId, message.UserId, message.Message, caution: true, ct)
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
