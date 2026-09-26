using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

/// <summary>
/// Removing a member. It is also how somebody leaves a group: the client sends this with itself
/// as the target, so there is no separate leave packet.
/// </summary>
public class KickMemberMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<KickMemberMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        KickMemberMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.GuildId <= 0 || message.PlayerId <= 0)
            return;

        var result = await _grainFactory
            .GetGuildGrain(message.GuildId)
            .KickAsync(ctx.PlayerId, message.PlayerId, message.Block, ct)
            .ConfigureAwait(false);

        if (
            !await ctx.SendGuildMemberMgmtFailureAsync(message.GuildId, result, ct)
                .ConfigureAwait(false)
        )
            return;

        if (message.PlayerId == ctx.PlayerId)
        {
            // They left: their own details window still shows a leave button until it refreshes.
            await ctx.SendComposerAsync(
                    new GroupDetailsChangedMessageComposer { GuildId = message.GuildId },
                    ct
                )
                .ConfigureAwait(false);
        }
    }
}
