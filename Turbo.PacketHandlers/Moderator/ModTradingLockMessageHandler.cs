using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.PacketHandlers.Moderator;

/// <summary>The mod action window's trade locks (<c>ModActionCtrl.as:288-291</c>): trading denied for the minutes sent, held to <c>command.tradelock</c> as :tradelock is. Answered with whether it was given (<c>ModerationMessageHandler.as:292-304</c> re-reads the user info on success).</summary>
[RequiresPermission(PermissionNodes.Moderation.TOOL)]
public class ModTradingLockMessageHandler(IModerationToolService moderation)
    : IMessageHandler<ModTradingLockMessage>
{
    private readonly IModerationToolService _moderation = moderation;

    public async ValueTask HandleAsync(
        ModTradingLockMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var given = await _moderation
            .SanctionAsync(
                ctx.PlayerId,
                message.UserId,
                new ModSanction(
                    ModSanctionType.TradeLock,
                    message.Message,
                    Minutes: message.Minutes
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
