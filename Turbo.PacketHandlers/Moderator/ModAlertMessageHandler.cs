using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.PacketHandlers.Moderator;

/// <summary>The mod action window's caution (<c>ModActionCtrl.as:256-263</c>, "Alert"), held to <c>command.warn</c> as the window holds it to alertPermission. Answered with whether it was given (<c>ModerationMessageHandler.as:292-304</c> re-reads the user info on success).</summary>
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
        var given = await _moderation
            .SanctionAsync(
                ctx.PlayerId,
                message.UserId,
                new ModSanction(ModSanctionType.Caution, message.Message),
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
