using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.PacketHandlers.Moderator;

/// <summary>The mod action window's "Mute 1h" (<c>ModActionCtrl.as:265-267</c>): a hotel silence for an hour, held to <c>command.silence</c> as :silence is. Answered with whether it was given (<c>ModerationMessageHandler.as:292-304</c> re-reads the user info on success).</summary>
[RequiresPermission(PermissionNodes.Moderation.TOOL)]
public class ModMuteMessageHandler(IModerationToolService moderation)
    : IMessageHandler<ModMuteMessage>
{
    private readonly IModerationToolService _moderation = moderation;

    public async ValueTask HandleAsync(
        ModMuteMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var given = await _moderation
            .SanctionAsync(
                ctx.PlayerId,
                message.UserId,
                new ModSanction(ModSanctionType.Mute, message.Message),
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
