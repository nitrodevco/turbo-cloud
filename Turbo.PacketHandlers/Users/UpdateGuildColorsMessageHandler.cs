using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

public class UpdateGuildColorsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<UpdateGuildColorsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        UpdateGuildColorsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.GuildId <= 0)
            return;

        await _grainFactory
            .GetGuildGrain(message.GuildId)
            .UpdateColorsAsync(ctx.PlayerId, message.PrimaryColorId, message.SecondaryColorId, ct)
            .ConfigureAwait(false);
    }
}
