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

        var updated = await _grainFactory
            .GetGuildGrain(message.GuildId)
            .UpdateColorsAsync(ctx.PlayerId, message.PrimaryColorId, message.SecondaryColorId, ct)
            .ConfigureAwait(false);

        if (!updated)
            return;

        // The group's furni is painted in these colours, so every room already holding a piece
        // of it is repainted. The group grain cannot do this itself; see
        // GuildFurniRefreshExtensions.
        await _grainFactory
            .RefreshGuildFurniEverywhereAsync(message.GuildId, ct)
            .ConfigureAwait(false);
    }
}
