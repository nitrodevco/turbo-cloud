using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

public class UpdateGuildIdentityMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<UpdateGuildIdentityMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        UpdateGuildIdentityMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.GuildId <= 0)
            return;

        await _grainFactory
            .GetGuildGrain(message.GuildId)
            .UpdateIdentityAsync(ctx.PlayerId, message.Name, message.Description, ct)
            .ConfigureAwait(false);
    }
}
