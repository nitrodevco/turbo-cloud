using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.PacketHandlers.Moderator;

/// <summary>The mod action window's bans (<c>ModActionCtrl.as:269-277</c>): a hotel ban for the length its sanction type names, held to <c>command.ban</c> as the window holds it to banPermission. Answered with whether it was given (<c>ModerationMessageHandler.as:292-304</c> re-reads the user info on success).</summary>
[RequiresPermission(PermissionNodes.Moderation.TOOL)]
public class ModBanMessageHandler(IModerationToolService moderation)
    : IMessageHandler<ModBanMessage>
{
    private readonly IModerationToolService _moderation = moderation;

    public async ValueTask HandleAsync(
        ModBanMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var given = await _moderation
            .SanctionAsync(
                ctx.PlayerId,
                message.UserId,
                new ModSanction(
                    ModSanctionType.Ban,
                    message.Message,
                    BanTypeId: message.SanctionTypeId
                ),
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
