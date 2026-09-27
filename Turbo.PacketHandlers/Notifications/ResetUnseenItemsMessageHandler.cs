using System;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Messages.Incoming.Notifications;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Notifications;

/// <summary>An inventory tab was opened; its items stop being new.</summary>
public class ResetUnseenItemsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<ResetUnseenItemsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        ResetUnseenItemsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || !Enum.IsDefined((UnseenItemCategory)message.Category))
            return;

        await _grainFactory
            .GetPlayerUnseenItemsGrain(ctx.PlayerId)
            .ResetCategoryAsync((UnseenItemCategory)message.Category, ct)
            .ConfigureAwait(false);
    }
}
