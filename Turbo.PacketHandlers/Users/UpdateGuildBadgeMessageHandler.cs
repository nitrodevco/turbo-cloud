using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

public class UpdateGuildBadgeMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<UpdateGuildBadgeMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        UpdateGuildBadgeMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.GuildId <= 0)
            return;

        var updated = await _grainFactory
            .GetGuildGrain(message.GuildId)
            .UpdateBadgeAsync(ctx.PlayerId, message.BadgeParts, ct)
            .ConfigureAwait(false);

        if (!updated)
            return;

        // The group's furni wears its badge, so every room already holding a piece of it is
        // repainted. The group grain cannot do this itself; see GuildFurniRefreshExtensions.
        await _grainFactory
            .RefreshGuildFurniEverywhereAsync(message.GuildId, ct)
            .ConfigureAwait(false);
    }
}
