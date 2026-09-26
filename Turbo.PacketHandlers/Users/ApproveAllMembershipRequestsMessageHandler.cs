using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

public class ApproveAllMembershipRequestsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<ApproveAllMembershipRequestsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        ApproveAllMembershipRequestsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.GuildId <= 0)
            return;

        var result = await _grainFactory
            .GetGuildGrain(message.GuildId)
            .ApproveAllRequestsAsync(ctx.PlayerId, ct)
            .ConfigureAwait(false);

        await ctx.SendGuildMemberMgmtFailureAsync(message.GuildId, result, ct)
            .ConfigureAwait(false);
    }
}
