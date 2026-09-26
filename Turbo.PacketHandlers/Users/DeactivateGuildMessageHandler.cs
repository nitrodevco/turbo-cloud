using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

public class DeactivateGuildMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<DeactivateGuildMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        DeactivateGuildMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.GuildId <= 0)
            return;

        var deleted = await _grainFactory
            .GetGuildGrain(message.GuildId)
            .DeactivateAsync(ctx.PlayerId, ct)
            .ConfigureAwait(false);

        if (!deleted)
            return;
    }
}
