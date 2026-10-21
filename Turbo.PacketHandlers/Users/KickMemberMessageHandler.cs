using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

/// <summary>
/// Removing a member. It is also how somebody leaves a group: the client sends this with itself
/// as the target, so there is no separate leave packet. The group tells the removed player and
/// the managers what changed.
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
        if (message.GuildId <= 0 || message.PlayerId <= 0)
            return;

        var result = await _grainFactory
            .GetGuildGrain(message.GuildId)
            .KickAsync(ctx.PlayerId, message.PlayerId, message.Block, ct)
            .ConfigureAwait(false);

        await ctx.SendGuildMemberMgmtFailureAsync(message.GuildId, result, ct)
            .ConfigureAwait(false);
    }
}
